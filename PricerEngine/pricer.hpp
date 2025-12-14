#pragma once

#include <iostream>
#include <nlohmann/json.hpp>
#include "pnl/pnl_vector.h"
#include "pnl/pnl_matrix.h"
#include "pnl/pnl_random.h"
#include "MultiFlowCallOption.hpp"
#include "BlackScholesModel.hpp"


class BlackScholesPricer {
public:
    PnlMat *volatility;
    PnlVect *paymentDates;
    PnlVect *strikes;
    int nAssets;
    double interestRate;
    double fdStep;
    int nSamples;
    double T;
    MultiFlowCallOption *opt;
    BlackScholesModel *model;
    PnlRng *rng;

    BlackScholesPricer(nlohmann::json &jsonParams);
    ~BlackScholesPricer();
    void priceAndDeltas(const PnlMat *past, double currentDate, bool isMonitoringDate,
                        double &price, double &priceStdDev, PnlVect* &deltas, PnlVect* &deltasStdDev);
    void print();
};
