#include <iostream>
#include <cmath>
#include <ctime>
#include <algorithm>
#include "json_reader.hpp"
#include "pricer.hpp"
#include "ConditionalBasketOption.hpp"
#include "ConditionalMaxOption.hpp"

BlackScholesPricer::BlackScholesPricer(nlohmann::json &jsonParams)
{
    jsonParams.at("VolCholeskyLines").get_to(volatility);
    jsonParams.at("MathPaymentDates").get_to(paymentDates);
    jsonParams.at("Strikes").get_to(strikes);
    jsonParams.at("DomesticInterestRate").get_to(interestRate);
    jsonParams.at("RelativeFiniteDifferenceStep").get_to(fdStep);
    jsonParams.at("SampleNb").get_to(nSamples);

    std::string payoffType = "ConditionalBasket";
    if (jsonParams.contains("PayoffType")) {
        jsonParams.at("PayoffType").get_to(payoffType);
    }

    nAssets = volatility->n;
    int nbTimeSteps = paymentDates->size;
    T = pnl_vect_get(paymentDates, nbTimeSteps - 1);

    // Create the appropriate option type based on PayoffType
    if (payoffType == "ConditionalMax") {
        opt = new ConditionalMaxOption(T, nbTimeSteps, nAssets, interestRate, strikes, paymentDates);
    } else {
        opt = new ConditionalBasketOption(T, nbTimeSteps, nAssets, interestRate, strikes, paymentDates);
    }

    model = new BlackScholesModel(jsonParams);
    rng = pnl_rng_create(PNL_RNG_MERSENNE);
    pnl_rng_sseed(rng, time(NULL));
}

BlackScholesPricer::~BlackScholesPricer()
{
    pnl_vect_free(&paymentDates);
    pnl_vect_free(&strikes);
    pnl_mat_free(&volatility);
    pnl_rng_free(&rng);
    delete model;
    delete opt;
}

void BlackScholesPricer::print()
{
    std::cout << "nAssets: " << nAssets << std::endl;
    std::cout << "fdStep: " << fdStep << std::endl;
    std::cout << "nSamples: " << nSamples << std::endl;
    std::cout << "strikes: ";
    pnl_vect_print_asrow(strikes);
    std::cout << "paymentDates: ";
    pnl_vect_print_asrow(paymentDates);
}

void BlackScholesPricer::priceAndDeltas(const PnlMat *past, double currentDate, bool isMonitoringDate,
                                        double &price, double &priceStdDev,
                                        PnlVect* &deltas, PnlVect* &deltasStdDev)
{
    price = 0.0;
    priceStdDev = 0.0;
    deltas = pnl_vect_create_from_zero(nAssets);
    deltasStdDev = pnl_vect_create_from_zero(nAssets);

    // Matrices de travail
    PnlMat *path      = pnl_mat_create(opt->nbTimeSteps + 1, nAssets);
    PnlMat *shiftPath = pnl_mat_create(opt->nbTimeSteps + 1, nAssets);

    // --- Choix lastIndex (je conserve ta logique, même si elle est discutable) ---
    int lastIndex;
    if (currentDate == 0.0) {
        lastIndex = 0;
    } else if (isMonitoringDate) {
        lastIndex = past->m - 1;
    } else {
        lastIndex = std::max(0, past->m - 2);
    }

    // --- Si produit déjà payé/éteint selon l'historique, continuation = 0 ---
    double alreadyAmount = 0.0;
    int alreadyPayIndex = -1;
    if (opt->alreadyPaidFromPast(past, lastIndex, isMonitoringDate, alreadyAmount, alreadyPayIndex)) {
        price = 0.0;
        priceStdDev = 0.0;
        pnl_vect_set_zero(deltas);
        pnl_vect_set_zero(deltasStdDev);
        pnl_mat_free(&path);
        pnl_mat_free(&shiftPath);
        return;
    }

    // Accumulateurs prix
    double sumV  = 0.0;
    double sumV2 = 0.0;

    // Accumulateurs deltas (pour stddev MC)
    std::vector<double> sumDelta(nAssets, 0.0);
    std::vector<double> sumDelta2(nAssets, 0.0);

    // Spot "courant" utilisé pour normaliser la FD relative
    // IMPORTANT : doit correspondre au point que shift_asset() modifie.
    std::vector<double> spot0(nAssets, 0.0);
    for (int d = 0; d < nAssets; ++d) {
        spot0[d] = pnl_mat_get(past, past->m - 1, d);
        if (spot0[d] <= 0.0) {
            // FD relative impossible si spot <= 0
            pnl_mat_free(&path);
            pnl_mat_free(&shiftPath);
            throw std::runtime_error("Spot must be > 0 for relative finite differences.");
        }
    }

    for (int j = 0; j < nSamples; ++j) {

        // Simule un chemin conditionnel à partir de past/currentDate
        model->asset(past, currentDate, lastIndex, path, rng);

        // --- Prix : cashflow + date de paiement -> discount vers currentDate ---
        double amount = 0.0;
        int payIndex = -1;
        opt->payoffAndPayIndex(path, amount, payIndex);

        double V = 0.0;
        if (payIndex >= 0) {
            const double tpay = pnl_vect_get(paymentDates, payIndex);
            V = amount * std::exp(-interestRate * (tpay - currentDate));
        }

        sumV  += V;
        sumV2 += V * V;

        // --- Deltas : FD relative, mais avec discount dépendant de la date de paiement ---
        for (int d = 0; d < nAssets; ++d) {

            // + shift
            pnl_mat_clone(shiftPath, path);
            model->shift_asset(d, lastIndex, 1.0 + fdStep, shiftPath);

            double aP = 0.0;
            int idxP = -1;
            opt->payoffAndPayIndex(shiftPath, aP, idxP);

            double VP = 0.0;
            if (idxP >= 0) {
                const double tpayP = pnl_vect_get(paymentDates, idxP);
                VP = aP * std::exp(-interestRate * (tpayP - currentDate));
            }

            // - shift
            pnl_mat_clone(shiftPath, path);
            model->shift_asset(d, lastIndex, 1.0 - fdStep, shiftPath);

            double aM = 0.0;
            int idxM = -1;
            opt->payoffAndPayIndex(shiftPath, aM, idxM);

            double VM = 0.0;
            if (idxM >= 0) {
                const double tpayM = pnl_vect_get(paymentDates, idxM);
                VM = aM * std::exp(-interestRate * (tpayM - currentDate));
            }

            // dérivée relative: dV/dS ≈ (V+ - V-) / (2*h*S)
            const double deriv = (VP - VM) / (2.0 * fdStep * spot0[d]);

            sumDelta[d]  += deriv;
            sumDelta2[d] += deriv * deriv;
        }
    }

    // --- Final : prix et stddev MC (erreur sur la moyenne) ---
    price = sumV / nSamples;
    const double EV2 = sumV2 / nSamples;
    priceStdDev = std::sqrt(std::fabs(EV2 - price * price) / nSamples);

    // --- Final : deltas et stddev MC ---
    for (int d = 0; d < nAssets; ++d) {
        const double delta = sumDelta[d] / nSamples;
        const double E2    = sumDelta2[d] / nSamples;
        pnl_vect_set(deltas, d, delta);
        pnl_vect_set(deltasStdDev, d, std::sqrt(std::fabs(E2 - delta * delta) / nSamples));
    }

    pnl_mat_free(&path);
    pnl_mat_free(&shiftPath);
}

