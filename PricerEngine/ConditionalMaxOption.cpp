#include "ConditionalMaxOption.hpp"
#include <cmath>
#include <algorithm>

ConditionalMaxOption::ConditionalMaxOption(double T_, int nbTimeSteps_, int size_, double r_,
                                           PnlVect *strikes_, PnlVect *dates_)
    : Option(T_, nbTimeSteps_, size_, r_, strikes_, dates_)
{
}

ConditionalMaxOption::~ConditionalMaxOption()
{
}

double ConditionalMaxOption::payoff(const PnlMat *path)
{

    // Only pay if the immediately previous payoff was 0

    double totalPayoff = 0.0;
    double prevPayoff = 0.0;

    for (int m = 0; m < nbTimeSteps; m++)
    {
        double discount = exp(r * (T - pnl_vect_get(dates, m)));
        double K = pnl_vect_get(strikes, m);

        double maxVal = pnl_mat_get(path, m + 1, 0);
        for (int n = 1; n < size; n++)
        {
            maxVal = std::max(maxVal, pnl_mat_get(path, m + 1, n));
        }

        double currentPayoff = 0.0;
        if (prevPayoff == 0.0)
        {
            currentPayoff = discount * std::max(maxVal - K, 0.0);
        }

        totalPayoff += currentPayoff;
        prevPayoff = currentPayoff;
    }

    return totalPayoff;
}
