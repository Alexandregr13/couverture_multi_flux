#pragma once

#include "Option.hpp"

class MultiFlowCallOption : public Option
{
public:
    MultiFlowCallOption(double T_, int nbTimeSteps_, int size_, double r_, PnlVect *strikes_, PnlVect *dates_);
    double payoff(const PnlMat *path) override;
    ~MultiFlowCallOption();
};
