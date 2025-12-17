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
    virtual void payoffAndPayIndex(const PnlMat* path, double& amount, int& payIndex) const = 0;
    virtual bool alreadyPaidFromPast(const PnlMat* past, int lastIndex, bool isMonitoringDate, double& amount, int& payIndex) const = 0;
};
