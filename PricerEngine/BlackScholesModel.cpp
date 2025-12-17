#include "BlackScholesModel.hpp"
#include <cmath>
#include <random>
#include <iostream>
#include <cassert>

BlackScholesModel::BlackScholesModel()
{
}

BlackScholesModel::BlackScholesModel(const nlohmann::json &jsonParams){
    jsonParams.at("VolCholeskyLines").get_to(volatility);
    jsonParams.at("MathPaymentDates").get_to(paymentDates);
    jsonParams.at("DomesticInterestRate").get_to(interestRate);
    nAssets = volatility->n;
    G = pnl_vect_create_from_zero(nAssets);
    vectVol = pnl_vect_create_from_zero(nAssets);

    for (int d = 0; d < nAssets; d++)
    {
        PnlVect L_d = pnl_vect_wrap_mat_row(volatility, d);
        pnl_vect_set(vectVol,d,pnl_vect_norm_two(&L_d));
    }

}



BlackScholesModel::~BlackScholesModel()
{
    pnl_vect_free(&vectVol);
    pnl_vect_free(&paymentDates);
    pnl_vect_free(&G);
    pnl_mat_free(&volatility);
}


void BlackScholesModel::asset(const PnlMat *past, double t, int last_index,
                             PnlMat *path, PnlRng *rng)
{
    int D = nAssets;
    double r = interestRate;

    // --- Cas : tout est déjà connu ---
    if (last_index == path->m - 1) {
        for (int i = 0; i < path->m; ++i) {
            for (int d = 0; d < D; ++d) {
                MLET(path, i, d) = MGET(past, i, d);
            }
        }
        return;
    }

    // --- Copier l'historique connu ---
    for (int i = 0; i <= last_index; ++i) {
        for (int d = 0; d < D; ++d) {
            MLET(path, i, d) = MGET(past, i, d);
        }
    }

    // --- Premier pas : t → paymentDates[last_index+1] ---
    pnl_vect_rng_normal(G, D, rng);
    double dt = GET(paymentDates, last_index + 1) - t;

    for (int d = 0; d < D; ++d) {
        PnlVect Ld = pnl_vect_wrap_mat_row(volatility, d);
        double sigma = GET(vectVol, d);
        double S0 = MGET(past, last_index, d);

        MLET(path, last_index + 1, d) =
            S0 * exp((r - 0.5 * sigma * sigma) * dt
                     + sqrt(dt) * pnl_vect_scalar_prod(&Ld, G));
    }

    // --- Pas suivants ---
    for (int i = last_index + 2; i < path->m; ++i) {
        pnl_vect_rng_normal(G, D, rng);
        double dt_i = GET(paymentDates, i - 1) - GET(paymentDates, i - 2);

        for (int d = 0; d < D; ++d) {
            PnlVect Ld = pnl_vect_wrap_mat_row(volatility, d);
            double sigma = GET(vectVol, d);
            double Sprev = MGET(path, i - 1, d);

            MLET(path, i, d) =
                Sprev * exp((r - 0.5 * sigma * sigma) * dt_i
                            + sqrt(dt_i) * pnl_vect_scalar_prod(&Ld, G));
        }
    }
}




void BlackScholesModel::shift_asset(int d, int lastIndex, double h, PnlMat *path)
{
    for (int i = lastIndex + 1; i < path->m; i++)
    {
        MLET(path, i, d) *= h;
    };
}
