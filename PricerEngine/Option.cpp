#include "Option.hpp"

Option::Option(double T_, int nbTimeSteps_, int size_, PnlVect *strikes_, PnlVect *dates_)
{
    T = T_;
    nbTimeSteps = nbTimeSteps_;
    size = size_;
    strikes = pnl_vect_copy(strikes_);
    dates = pnl_vect_copy(dates_);
}

Option::~Option()
{
    pnl_vect_free(&strikes);
    pnl_vect_free(&dates);
}
