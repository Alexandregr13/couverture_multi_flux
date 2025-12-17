#pragma once

#include "pnl/pnl_vector.h"
#include "pnl/pnl_matrix.h"
#include "Capitalization.hpp"

class Option
{
public:
    double T;
    int nbTimeSteps;
    int size;
    PnlVect *strikes;
    PnlVect *dates;

    Option(double T_, int nbTimeSteps_, int size_, PnlVect *strikes_, PnlVect *dates_);
    virtual ~Option();
    virtual double payoff(const PnlMat *path, CapitalizationFunc capitalize) = 0;
};
