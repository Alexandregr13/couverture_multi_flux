#pragma once

#include "pnl/pnl_vector.h"
#include "pnl/pnl_matrix.h"
#include "Capitalization.hpp"

class Option
{
public:
    double T; // maturité
    int nbTimeSteps; // nombre de step
    int size; // dimsnesion de l'opition
    PnlVect *strikes; // strike 
    PnlVect *dates;  // dates de paiement de l'opiton

    Option(double T_, int nbTimeSteps_, int size_, PnlVect *strikes_, PnlVect *dates_);
    virtual ~Option();
    virtual double payoff(const PnlMat *path, CapitalizationFunc capitalize) = 0;
};
