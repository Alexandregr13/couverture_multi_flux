#pragma once

#include "Option.hpp"

class ConditionalBasketOption : public Option
{
public:
    ConditionalBasketOption(double T_, int nbTimeSteps_, int size_,
                            PnlVect *strikes_, PnlVect *dates_);
    ~ConditionalBasketOption();
    double payoff(const PnlMat *path, CapitalizationFunc capitalize) override;
};
