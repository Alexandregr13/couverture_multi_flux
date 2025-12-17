#include "BlackScholesModel.hpp"
#include <cmath>

BlackScholesModel::BlackScholesModel(int nAssets_, double interestRate_, PnlMat *volatility_)
    : nAssets(nAssets_), interestRate(interestRate_)
{
    volatility = pnl_mat_copy(volatility_);
    G = pnl_vect_create_from_zero(nAssets);
    vectVol = pnl_vect_create_from_zero(nAssets);

    for (int d = 0; d < nAssets; d++)
    {
        PnlVect L_d = pnl_vect_wrap_mat_row(volatility, d);
        pnl_vect_set(vectVol, d, pnl_vect_norm_two(&L_d));
    }
}

BlackScholesModel::~BlackScholesModel()
{
    pnl_vect_free(&vectVol);
    pnl_vect_free(&G);
    pnl_mat_free(&volatility);
}

void BlackScholesModel::asset(const PnlMat *past, double t, int lastIndex,
                               const PnlVect *simulationDates, PnlMat *path, PnlRng *rng)
{
    double r = this->interestRate;

    // Si on est à la dernière date on copie past dans path
    if (lastIndex == path->m - 1)
    {
        pnl_mat_extract_subblock(path, past, 0, path->m, 0, path->n);
        return;
    }

    // Copie obs dans path
    pnl_mat_set_subblock(path, past, 0, 0);

    // premier pas de simulation de t vers simulationDates[lastIndex]
    pnl_vect_rng_normal(G, nAssets, rng);
    double dt = GET(simulationDates, lastIndex) - t;

    for (int d = 0; d < nAssets; d++)
    {
        PnlVect L_d = pnl_vect_wrap_mat_row(volatility, d);
        double sigma_d = GET(vectVol, d);
        double s_t_d = MGET(past, past->m - 1, d);
        MLET(path, lastIndex + 1, d) = s_t_d * exp((r - sigma_d * sigma_d / 2.0) * dt
                                                    + sqrt(dt) * pnl_vect_scalar_prod(&L_d, G));
    }

    // pas suivants entre les dates de simulation
    for (int i = lastIndex + 2; i < path->m; i++)
    {
        pnl_vect_rng_normal(G, nAssets, rng);
        double timeStep = GET(simulationDates, i - 1) - GET(simulationDates, i - 2);

        for (int d = 0; d < nAssets; d++)
        {
            PnlVect L_d = pnl_vect_wrap_mat_row(volatility, d);
            double sigma_d = GET(vectVol, d);
            double s_prev_d = MGET(path, i - 1, d);
            MLET(path, i, d) = s_prev_d * exp((r - sigma_d * sigma_d / 2.0) * timeStep
                                               + sqrt(timeStep) * pnl_vect_scalar_prod(&L_d, G));
        }
    }
}

void BlackScholesModel::shiftAsset(int d, int lastIndex, double h, PnlMat *path)
{
    for (int i = lastIndex + 1; i < path->m; i++)
    {
        MLET(path, i, d) *= h;
    }
}
