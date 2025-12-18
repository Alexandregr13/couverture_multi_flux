#include "ConditionalMaxOption.hpp"
#include <cmath>
#include <algorithm>

ConditionalMaxOption::ConditionalMaxOption(double T_, int nbTimeSteps_, int size_,
                                           PnlVect *strikes_, PnlVect *dates_)
    : Option(T_, nbTimeSteps_, size_, strikes_, dates_)
{
}

ConditionalMaxOption::~ConditionalMaxOption()
{
}

// Calcul du payoff pour l'option max conditionnelle (avec capitalisation jusqu'à T)
double ConditionalMaxOption::payoff(const PnlMat *path, CapitalizationFunc capitalize)
{
    double totalPayoff = 0.0;
    double prevPayoff = 0.0;

    for (int m = 0; m < nbTimeSteps; m++)
    {
        double K = pnl_vect_get(strikes, m);
        double t_m = pnl_vect_get(dates, m);

        double maxVal = pnl_mat_get(path, m + 1, 0);
        for (int n = 1; n < size; n++)
        {
            maxVal = std::max(maxVal, pnl_mat_get(path, m + 1, n));
        }

        double currentPayoff = 0.0;
        if (prevPayoff == 0.0)
        {
            currentPayoff = std::max(maxVal - K, 0.0);
        }

        totalPayoff += capitalize(currentPayoff, t_m);
        prevPayoff = currentPayoff;
    }

    return totalPayoff;
}
