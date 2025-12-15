#include "ConditionalBasketOption.hpp"
#include <cmath>
#include <algorithm>

ConditionalBasketOption::ConditionalBasketOption(double T_, int nbTimeSteps_, int size_, double r_,
                                                   PnlVect *strikes_, PnlVect *dates_)
    : Option(T_, nbTimeSteps_, size_, r_, strikes_, dates_)
{
}

ConditionalBasketOption::~ConditionalBasketOption()
{
}

double ConditionalBasketOption::payoff(const PnlMat *path)
{

    // Once any payoff is positive, stop

    double totalPayoff = 0.0;

    for (int m = 0; m < nbTimeSteps; m++)
    {
        if (totalPayoff > 0) break;

        double discount = exp(r * (T - pnl_vect_get(dates, m)));
        double K = pnl_vect_get(strikes, m);

        double sum = 0.0;
        for (int n = 0; n < size; n++)
        {
            sum += pnl_mat_get(path, m + 1, n);
        }
        double underlying = sum / size;

        double currentPayoff = discount * std::max(underlying - K, 0.0);
        totalPayoff += currentPayoff;
    }

    return totalPayoff;
}
