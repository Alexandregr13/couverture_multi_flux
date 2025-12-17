#include <iostream>
#include <cmath>
#include <algorithm>
#include "pricer.hpp"
#include "json_reader.hpp"
#include "Capitalization.hpp"
#include "ConditionalBasketOption.hpp"
#include "ConditionalMaxOption.hpp"

BlackScholesPricer::BlackScholesPricer(nlohmann::json &jsonParams) {
    PnlMat *volatility = nullptr;
    PnlVect *paymentDates = nullptr;
    PnlVect *strikes = nullptr;
    double interestRate;

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

    int nAssets = volatility->n;
    int nbTimeSteps = paymentDates->size;
    double T = pnl_vect_get(paymentDates, nbTimeSteps - 1);

    // Création du modèle 
    model = new BlackScholesModel(nAssets, interestRate, volatility);

    // Création de l'option
    if (payoffType == "ConditionalMax") {
        opt = new ConditionalMaxOption(T, nbTimeSteps, nAssets, strikes, paymentDates);
    } else {
        opt = new ConditionalBasketOption(T, nbTimeSteps, nAssets, strikes, paymentDates);
    }

    // Générateur aléatoire avec seed pour comparer nos résultats
    rng = pnl_rng_create(PNL_RNG_MERSENNE);
    pnl_rng_sseed(rng, 42);

    // Libération des données temporaires (copiées dans model et opt)
    pnl_mat_free(&volatility);
    pnl_vect_free(&paymentDates);
    pnl_vect_free(&strikes);
}

BlackScholesPricer::~BlackScholesPricer() {
    pnl_rng_free(&rng);
    delete model;
    delete opt;
}

void BlackScholesPricer::print() {
    std::cout << "=== Pricer ===" << std::endl;
    std::cout << "nAssets: " << model->nAssets << std::endl;
    std::cout << "interestRate: " << model->interestRate << std::endl;
    std::cout << "fdStep: " << fdStep << std::endl;
    std::cout << "nSamples: " << nSamples << std::endl;
    std::cout << "T: " << opt->T << std::endl;
    std::cout << "nbTimeSteps: " << opt->nbTimeSteps << std::endl;
    std::cout << "strikes: ";
    pnl_vect_print_asrow(opt->strikes);
    std::cout << "paymentDates: ";
    pnl_vect_print_asrow(opt->dates);
}

void BlackScholesPricer::priceAndDeltas(const PnlMat *past, double currentDate, bool isMonitoringDate,
                                         double &price, double &priceStdDev,
                                         PnlVect *&deltas, PnlVect *&deltasStdDev) {
    // Récupération des paramètres depuis model et opt
    int nAssets = model->nAssets;
    double r = model->interestRate;
    double T = opt->T;
    const PnlVect *simulationDates = opt->dates;

    price = 0.;
    priceStdDev = 0.;
    deltas = pnl_vect_create_from_zero(nAssets);
    deltasStdDev = pnl_vect_create_from_zero(nAssets);
    double esp = 0, esp2 = 0;

    PnlMat *path = pnl_mat_create(opt->nbTimeSteps + 1, nAssets);
    PnlMat *shiftPath = pnl_mat_create(opt->nbTimeSteps + 1, nAssets);

    double delta_d, payoff, payoff_plus, payoff_minus;

    // Détermination des indices
    int lastIndex;
    int shiftIdx;
    if (currentDate == 0.0) {
        lastIndex = 0;
        shiftIdx = -1;
    } else if (isMonitoringDate) {
        lastIndex = past->m - 1;
        shiftIdx = lastIndex - 1;
    } else {
        lastIndex = std::max(0, past->m - 2);
        shiftIdx = lastIndex;
    }

    //  capitalise un flux de t_m vers T
    auto capitalize = createCapitalization(r, T);

    // Boucle Montecarlo
    for (int j = 0; j < nSamples; j++) {
        model->asset(past, currentDate, lastIndex, simulationDates, path, rng);
        payoff = opt->payoff(path, capitalize);
        esp += payoff;
        esp2 += payoff * payoff;

        // Calcul delta
        for (int d = 0; d < nAssets; d++) {
            pnl_mat_clone(shiftPath, path);
            model->shiftAsset(d, shiftIdx, 1 + fdStep, shiftPath);
            payoff_plus = opt->payoff(shiftPath, capitalize);

            pnl_mat_clone(shiftPath, path);
            model->shiftAsset(d, shiftIdx, 1 - fdStep, shiftPath);
            payoff_minus = opt->payoff(shiftPath, capitalize);

            delta_d = payoff_plus - payoff_minus;
            pnl_vect_set(deltas, d, pnl_vect_get(deltas, d) + delta_d);
            pnl_vect_set(deltasStdDev, d, pnl_vect_get(deltasStdDev, d) + delta_d * delta_d);
        }
    }

    // Calcul du prix actualisé
    double exprT_t = exp(-r * (T - currentDate));
    esp /= nSamples;
    esp2 /= nSamples;
    price = exprT_t * esp;
    priceStdDev = sqrt(fabs((exprT_t * exprT_t * esp2 - price * price) / nSamples));

    // Calcul des deltas 
    double espDelta = exprT_t / (2 * fdStep * nSamples);
    double esp2Delta = espDelta * espDelta * nSamples;
    double st, fact;
    for (int d = 0; d < nAssets; d++) {
        st = pnl_mat_get(past, past->m - 1, d);
        delta_d = pnl_vect_get(deltas, d);
        pnl_vect_set(deltas, d, delta_d * espDelta / st);
        double tmp = delta_d / (2 * fdStep * nSamples * st);
        fact = pnl_vect_get(deltasStdDev, d) * (esp2Delta / (st * st)) - tmp * tmp;
        pnl_vect_set(deltasStdDev, d, sqrt(fabs(fact) / nSamples));
    }

    pnl_mat_free(&path);
    pnl_mat_free(&shiftPath);
}
