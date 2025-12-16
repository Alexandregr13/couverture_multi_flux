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
    price = 0.;
    priceStdDev = 0.;
    deltas = pnl_vect_create_from_zero(nAssets);
    deltasStdDev = pnl_vect_create_from_zero(nAssets);
    double esp = 0, esp2 = 0;

    PnlMat *path = pnl_mat_create(opt->nbTimeSteps + 1, nAssets);
    PnlMat *shiftPath = pnl_mat_create(opt->nbTimeSteps + 1, nAssets);

    double delta_d, payoff, payoff_plus, payoff_minus;

    int lastIndex;
    int shiftIdx; // j'ai rajouté
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

    for (int j = 0; j < nSamples; j++)
    {
        model->asset(past, currentDate, lastIndex, path, rng);
        payoff = opt->payoff(path);
        esp += payoff;
        esp2 += payoff * payoff;
        for (int d = 0; d < nAssets; d++)
        {
            pnl_mat_clone(shiftPath, path);
            model->shift_asset(d, shiftIdx, 1 + fdStep, shiftPath);//je passe shiftIdx au lieu de last
            payoff_plus = opt->payoff(shiftPath);
            pnl_mat_clone(shiftPath, path);
            model->shift_asset(d, shiftIdx, 1 - fdStep, shiftPath); 
            payoff_minus = opt->payoff(shiftPath);
            delta_d = payoff_plus - payoff_minus;
            pnl_vect_set(deltas, d, pnl_vect_get(deltas, d) + delta_d);
            pnl_vect_set(deltasStdDev, d, pnl_vect_get(deltasStdDev, d) + delta_d * delta_d);
        }
    }
    double exprT_t = exp(-interestRate * (T - currentDate));

    esp /= nSamples;
    esp2 /= nSamples;
    price = exprT_t * esp;
    priceStdDev = sqrt(fabs((exprT_t * exprT_t * esp2 - price * price) / nSamples));

    double espDelta = exprT_t / (2 * fdStep * nSamples);
    double esp2Delta = espDelta * espDelta * nSamples;
    double st, fact;
    for (int d = 0; d < opt->size; d++)
    {
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
