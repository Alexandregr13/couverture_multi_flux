#ifndef BLACK_SCHOLES_MODEL_HPP
#define BLACK_SCHOLES_MODEL_HPP
#include "pnl/pnl_matvect.h"
#include "pnl/pnl_vector.h"
#include "pnl/pnl_matrix.h"
#include "pnl/pnl_random.h"
#include "json_reader.hpp"


class BlackScholesModel
{
public:
    int nAssets;       /// nombre d'actifs du modèle
    double interestRate; /// taux d'intérêt
    PnlMat *volatility;  /// matrice  de Chlosky
    PnlVect *G;           /// Vector for simulation
    PnlVect *paymentDates; // pour lz calcul de timestep
    PnlVect *vectVol;  // vect of volatility

public:
    BlackScholesModel();
    BlackScholesModel(const nlohmann::json &jsonParams);
    ~BlackScholesModel();

    /**
     * Génère une trajectoire du modèle et la stocke dans path
     *
     * @param[out] path contient une trajectoire du modèle. C'est une matrice de taille (N+1) x D
     * @param[in] path : matrice de taille (N+1)xD
     * @param[in] past : matrice de taille (i+1)*D avec i last-index
     * @param[in] t  :  temps actuel
     * @param[in] rng : génerateur des nombres aléatoires
     */
    void asset(const PnlMat *past, double t, int last_index, PnlMat *path, PnlRng *rng);

    /**
     * simuler 2 trajectoires utilisant les mêmes aléas Browniens mais shiftées l'une par rapport à l'autre
     *
     * @param[out] path : multiplie la d-éme colone par (1+h)
     * @param[in] d : index of colonne
     * @param[in] h : double
     * @param[in] int : double
     * @param[in] path : matrice de taille (N + 1)*D qui contient la simulation du modéle
     */
    void shift_asset(int d, int lastIndex, double h, PnlMat *path);

};
#endif
