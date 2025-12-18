#ifndef BLACK_SCHOLES_MODEL_HPP
#define BLACK_SCHOLES_MODEL_HPP
#include "pnl/pnl_matvect.h"
#include "pnl/pnl_vector.h"
#include "pnl/pnl_matrix.h"
#include "pnl/pnl_random.h"


class BlackScholesModel
{
public:
    int nAssets;
    double interestRate;
    PnlMat *volatility;
    PnlVect *vectVol;
    PnlVect *G;

public:
    BlackScholesModel(int nAssets_, double interestRate_, PnlMat *volatility_);
    ~BlackScholesModel();


    void asset(const PnlMat *past, double t, int lastIndex,
               const PnlVect *simulationDates, PnlMat *path, PnlRng *rng);


    void shiftAsset(int d, int lastIndex, double h, PnlMat *path);
};
#endif
