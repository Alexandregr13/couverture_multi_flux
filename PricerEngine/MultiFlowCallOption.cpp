#include "MultiFlowCallOption.hpp"
#include <cmath>

MultiFlowCallOption::MultiFlowCallOption(double T_, int nbTimeSteps_, int size_, double r_, PnlVect *strikes_, PnlVect *dates_)
    : Option(T_, nbTimeSteps_, size_, r_, strikes_, dates_)
{
}

MultiFlowCallOption::~MultiFlowCallOption()
{
}

double MultiFlowCallOption::payoff(const PnlMat *path)
{
    double res = 0.0, exponential;
    bool active = true;
    int i = 0;
    while (active && i < nbTimeSteps)
    {
        exponential = exp(r * (T - pnl_vect_get(dates, i)));
        res = exponential * std::max(pnl_mat_get(path, i + 1, i) - pnl_vect_get(strikes, i), 0.0);
        if (res > 0) active = false;
        i++;
    }
    return res;
}
