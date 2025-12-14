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


void BlackScholesModel::asset(const PnlMat *past, double t,int last_index , PnlMat *path, PnlRng *rng)
{
    int D = this->nAssets;
    double r = this->interestRate;

    if (last_index == path->m - 1)
    {
        pnl_mat_extract_subblock(path, past, 0, path->m, 0, path->n);
        return;
    }


    pnl_mat_set_subblock(path, past, 0, 0);

    pnl_vect_rng_normal(G, D, rng);
    double dt = GET(paymentDates, last_index ) - t;

    for (int d = 0; d < D; d++)
    {
        PnlVect L_d = pnl_vect_wrap_mat_row(volatility, d);
        double sigma_d = GET(vectVol,d);
        double s_t_d = MGET(past, past->m - 1, d);
        MLET(path, last_index + 1 , d) = s_t_d * exp((r - sigma_d * sigma_d / 2.0) * dt + sqrt(dt) * pnl_vect_scalar_prod(&L_d, G));
    }


    for (int i = last_index + 2; i < path->m; i++)
    {
        pnl_vect_rng_normal(G, D, rng);

        double time_step = GET(paymentDates, i - 1) - GET(paymentDates, i - 2);
        for (int d = 0; d < D; d++)
        {
            PnlVect L_d = pnl_vect_wrap_mat_row(volatility, d);
            double sigma_d = GET(vectVol,d);
            double s_t_d = MGET(path, i - 1, d);
            MLET(path, i, d) = s_t_d * exp((r - sigma_d * sigma_d / 2.0) * time_step +  sqrt(time_step) * pnl_vect_scalar_prod(&L_d, G));
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
