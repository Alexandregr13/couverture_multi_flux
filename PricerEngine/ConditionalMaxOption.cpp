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

// double ConditionalMaxOption::payoff(const PnlMat *path)
// {

//     // Only pay if the immediately previous payoff was 0

//     double totalPayoff = 0.0;
//     double prevPayoff = 0.0;

//     for (int m = 0; m < nbTimeSteps; m++)
//     {
//         double K = pnl_vect_get(strikes, m);

//         double maxVal = pnl_mat_get(path, m + 1, 0);
//         for (int n = 1; n < size; n++)
//         {
//             maxVal = std::max(maxVal, pnl_mat_get(path, m + 1, n));
//         }

//         double currentPayoff = 0.0;
//         if (prevPayoff == 0.0)
//         {
//             currentPayoff = std::max(maxVal - K, 0.0);
//         }

//         totalPayoff += currentPayoff;
//         prevPayoff = currentPayoff;
//     }

//     return totalPayoff;
// }

void ConditionalMaxOption::payoffAndPayIndex(const PnlMat* path, double& amount, int& payIndex) const
{
    amount = 0.0;
    payIndex = -1;

    double prev = 0.0;
    for (int m = 0; m < dates->size; ++m) {
        double maxVal = -1e300;
        for (int d = 0; d < size; ++d) {
            maxVal = std::max(maxVal, MGET(path, m + 1, d));
        }

        const double K = pnl_vect_get(strikes, m);
        const double cf = std::max(maxVal - K, 0.0);

        if (prev <= 0.0 && cf > 0.0) {
            amount = cf;
            payIndex = m;
            return;
        }
        prev = cf;
    }
}

bool ConditionalMaxOption::alreadyPaidFromPast(const PnlMat* past,
                                               int lastIndex,
                                               bool isMonitoringDate,
                                               double& amount,
                                               int& payIndex) const
{
    amount = 0.0;
    payIndex = -1;

    const int maxM = std::min(lastIndex, dates->size - 1);
    if (maxM < 0) return false;

    double prev = 0.0;
    for (int m = 0; m <= maxM; ++m) {
        if (!isMonitoringDate && m == lastIndex) break;

        double maxVal = -1e300;
        for (int d = 0; d < size; ++d) {
            maxVal = std::max(maxVal, MGET(past, m + 1, d));
        }

        const double K = pnl_vect_get(strikes, m);
        const double cf = std::max(maxVal - K, 0.0);

        if (prev <= 0.0 && cf > 0.0) {
            amount = cf;
            payIndex = m;
            return true;
        }
        prev = cf;
    }
    return false;
}
