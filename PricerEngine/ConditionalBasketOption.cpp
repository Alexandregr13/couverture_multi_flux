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

// double ConditionalBasketOption::payoff(const PnlMat *path)
// {

//     // Once any payoff is positive, stop

//     double totalPayoff = 0.0;

//     for (int m = 0; m < nbTimeSteps; m++)
//     {
//         if (totalPayoff > 0) break;

//         double K = pnl_vect_get(strikes, m);

//         double sum = 0.0;
//         for (int n = 0; n < size; n++)
//         {
//             sum += pnl_mat_get(path, m + 1, n);
//         }
//         double underlying = sum / size;

//         double currentPayoff = std::max(underlying - K, 0.0);
//         totalPayoff += currentPayoff;
//     }

//     return totalPayoff;
// }


void ConditionalBasketOption::payoffAndPayIndex(const PnlMat* path, double& amount, int& payIndex) const
{
    amount = 0.0;
    payIndex = -1;

    for (int m = 0; m < dates->size; ++m) {
        double underlying = 0.0;
        for (int d = 0; d < size; ++d) {
            underlying += MGET(path, m + 1, d);
        }
        underlying /= (double)size;

        const double K = pnl_vect_get(strikes, m);
        const double cf = std::max(underlying - K, 0.0);

        if (cf > 0.0) {
            amount = cf;
            payIndex = m;
            return;
        }
    }
}

bool ConditionalBasketOption::alreadyPaidFromPast(const PnlMat* past,
                                                  int lastIndex,
                                                  bool isMonitoringDate,
                                                  double& amount,
                                                  int& payIndex) const
{
    amount = 0.0;
    payIndex = -1;

    const int maxM = std::min(lastIndex, dates->size - 1);
    if (maxM < 0) return false;

    for (int m = 0; m <= maxM; ++m) {
        if (!isMonitoringDate && m == lastIndex) break;

        double underlying = 0.0;
        for (int d = 0; d < size; ++d) {
            underlying += MGET(past, m + 1, d);
        }
        underlying /= (double)size;

        const double K = pnl_vect_get(strikes, m);
        const double cf = std::max(underlying - K, 0.0);

        if (cf > 0.0) {
            amount = cf;
            payIndex = m;
            return true;
        }
    }
    return false;
}
