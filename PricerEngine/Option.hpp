#pragma once

#include "pnl/pnl_vector.h"
#include "pnl/pnl_matrix.h"

class Option
{
public:
    double T;
    int nbTimeSteps;
    int size;
    double r;
    PnlVect *strikes;
    PnlVect *dates;

    Option(double T_, int nbTimeSteps_, int size_, double r_, PnlVect *strikes_, PnlVect *dates_);
    virtual ~Option();
    virtual double payoff(const PnlMat *path) = 0;
};
