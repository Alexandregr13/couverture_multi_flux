#include "ConditionalBasketOption.hpp"
#include <cmath>
#include <algorithm>

ConditionalBasketOption::ConditionalBasketOption(double T_, int nbTimeSteps_, int size_,
                                                   PnlVect *strikes_, PnlVect *dates_)
    : Option(T_, nbTimeSteps_, size_, strikes_, dates_)
{
}

ConditionalBasketOption::~ConditionalBasketOption()
{
}

// Calcul du payoff pour l'option panier conditionnelle (avec capitalisation jusqu'à T)
double ConditionalBasketOption::payoff(const PnlMat *path, CapitalizationFunc capitalize)
{
    double totalPayoff = 0.0;

    for (int m = 0; m < nbTimeSteps; m++)
    {
        if (totalPayoff > 0) break;

        double K = pnl_vect_get(strikes, m);
        double t_m = pnl_vect_get(dates, m);

        double sum = 0.0;
        for (int n = 0; n < size; n++)
        {
            sum += pnl_mat_get(path, m + 1, n);
        }
        double underlying = sum / size;

        double currentPayoff = std::max(underlying - K, 0.0);
        totalPayoff += capitalize(currentPayoff, t_m);
    }

    return totalPayoff;
}
