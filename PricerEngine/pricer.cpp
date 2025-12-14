#include <iostream>
#include <cmath>
#include "json_reader.hpp"
#include "pricer.hpp"

BlackScholesPricer::BlackScholesPricer(nlohmann::json &jsonParams) {
    jsonParams.at("MathPaymentDates").get_to(paymentDates);
    jsonParams.at("SampleNb").get_to(nSamples);
    jsonParams.at("RelativeFiniteDifferenceStep").get_to(fdStep);
    model = new BlackScholesModel(jsonParams);
    option = instance_option(jsonParams);
    rng = pnl_rng_create(PNL_RNG_MERSENNE);
    pnl_rng_sseed(rng, time(NULL));
}

BlackScholesPricer::~BlackScholesPricer() {
    pnl_vect_free(&paymentDates);
    pnl_rng_free(&rng);
    delete model;
    delete option;
}

void BlackScholesPricer::print() {
    std::cout << "nAssets: " << model->nAssets << std::endl;
    std::cout << "fdStep: " << fdStep << std::endl;
    std::cout << "nSamples: " << nSamples << std::endl;
    std::cout << "strikes: ";
    pnl_vect_print_asrow(option->strike);
    std::cout << "paymentDates: ";
    pnl_vect_print_asrow(paymentDates);
    std::cout << "volatility: ";
    pnl_mat_print(model->volatility);
}

void BlackScholesPricer::priceAndDeltas(const PnlMat *past, double currentDate, bool isMonitoringDate,
                                         double &price, double &priceStdDev,
                                         PnlVect* &deltas, PnlVect* &deltasStdDev) {
    const int D = model->nAssets;
    const int N = option->strike->size;
    const double T = GET(paymentDates, paymentDates->size - 1);
    const double r = model->interestRate;
    const double h = fdStep;

    // Determine last observation index
    int lastIndex = isMonitoringDate ? past->m - 1 : past->m - 2;
    if (currentDate == 0.0) {
        lastIndex = 0;
    }

    // Get current spot prices
    PnlVect *St = pnl_vect_create(D);
    pnl_mat_get_row(St, past, past->m - 1);

    // Accumulators
    double payoffSum = 0.0;
    double payoffSum2 = 0.0;
    PnlVect *sumXi = pnl_vect_create_from_scalar(D, 0.0);
    PnlVect *sumXi2 = pnl_vect_create_from_scalar(D, 0.0);

    // Allocate path matrices
    PnlMat *path = pnl_mat_create(N + 1, D);
    PnlMat *pathPlus = pnl_mat_create(N + 1, D);
    PnlMat *pathMinus = pnl_mat_create(N + 1, D);

    // Monte Carlo loop
    for (int j = 0; j < nSamples; ++j) {
        model->asset(past, currentDate, lastIndex, path, rng);

        double payoff = option->payOff(path);
        payoffSum += payoff;
        payoffSum2 += payoff * payoff;

        // Delta calculation with finite differences
        for (int d = 0; d < D; ++d) {
            pnl_mat_clone(pathPlus, path);
            pnl_mat_clone(pathMinus, path);

            // Shift asset d from lastIndex+1 onwards
            for (int i = lastIndex + 1; i < path->m; ++i) {
                double base = MGET(path, i, d);
                MLET(pathPlus, i, d) = base * (1.0 + h);
                MLET(pathMinus, i, d) = base * (1.0 - h);
            }

            double payoffPlus = option->payOff(pathPlus);
            double payoffMinus = option->payOff(pathMinus);

            double Std = GET(St, d);
            double xi = std::exp(-r * (T - currentDate)) * (payoffPlus - payoffMinus) / (2.0 * h * Std);

            LET(sumXi, d) = GET(sumXi, d) + xi;
            LET(sumXi2, d) = GET(sumXi2, d) + xi * xi;
        }
    }

    // Compute price and stddev
    double mean = payoffSum / nSamples;
    price = std::exp(-r * (T - currentDate)) * mean;

    double var = std::max(0.0, payoffSum2 / nSamples - mean * mean);
    double stddev = std::sqrt(var);
    priceStdDev = 1.96 * std::exp(-r * (T - currentDate)) * stddev / std::sqrt(nSamples);

    // Compute deltas and stddevs
    deltas = pnl_vect_create(D);
    deltasStdDev = pnl_vect_create(D);

    for (int d = 0; d < D; ++d) {
        double meanXi = GET(sumXi, d) / nSamples;
        double meanXi2 = GET(sumXi2, d) / nSamples;

        double varXi = std::max(0.0, (meanXi2 - meanXi * meanXi) / nSamples);
        double stddevXi = std::sqrt(varXi);

        LET(deltas, d) = meanXi;
        LET(deltasStdDev, d) = stddevXi;
    }

    // Cleanup
    pnl_mat_free(&path);
    pnl_mat_free(&pathPlus);
    pnl_mat_free(&pathMinus);
    pnl_vect_free(&sumXi);
    pnl_vect_free(&sumXi2);
    pnl_vect_free(&St);
}
