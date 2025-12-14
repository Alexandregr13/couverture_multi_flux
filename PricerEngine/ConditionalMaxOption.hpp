#pragma once

#include "Option.hpp"

class ConditionalMaxOption : public Option
{
public:
    ConditionalMaxOption(double T_, int nbTimeSteps_, int size_, double r_,
                         PnlVect *strikes_, PnlVect *dates_);
    ~ConditionalMaxOption();
    double payoff(const PnlMat *path) override;
};
