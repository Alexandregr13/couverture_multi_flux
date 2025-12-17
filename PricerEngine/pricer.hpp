#pragma once

#include <nlohmann/json.hpp>
#include "pnl/pnl_vector.h"
#include "pnl/pnl_matrix.h"
#include "pnl/pnl_random.h"
#include "Option.hpp"
#include "BlackScholesModel.hpp"

class BlackScholesPricer {
public:
    BlackScholesModel *model;  /// Modèle de diffusion (paramètres marché)
    Option *opt;               /// Option à pricer (caractéristiques contrat)
    double fdStep;             /// Pas pour différences finies
    int nSamples;              /// Nombre de simulations Monte Carlo
    PnlRng *rng;               /// Générateur de nombres aléatoires

    BlackScholesPricer(nlohmann::json &jsonParams);
    ~BlackScholesPricer();

    void priceAndDeltas(const PnlMat *past, double currentDate, bool isMonitoringDate,
                        double &price, double &priceStdDev, PnlVect* &deltas, PnlVect* &deltasStdDev);
    void print();
};
