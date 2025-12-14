#include "ConditionalMaxOption.hpp"

ConditionalMaxOption::ConditionalMaxOption()
{
}

ConditionalMaxOption::ConditionalMaxOption(const nlohmann::json &json) : Option(json)
{

}

ConditionalMaxOption::~ConditionalMaxOption()
{

}

double ConditionalMaxOption::payOff(const PnlMat *matrix)
{
    double T = GET(paymentDate , paymentDate->size - 1) ;
    double payOff = 0.;
    double p_prec = 0.;

    for (size_t m = 1 ; m < matrix->m; m++)
    {
        PnlVect valSoujacent = pnl_vect_wrap_mat_row(matrix, m);
        double Pm = pnl_vect_max(&valSoujacent);
        int is_prec_null = p_prec == 0.0 ? 1 : 0;
        Pm = std::max(Pm - GET(strike, m - 1), 0.) *(double (is_prec_null));

        double facteur = std::exp(intersertRate*(T - GET(paymentDate , m - 1))) ;
        payOff += Pm*facteur;
        p_prec = Pm ;

    }

    return payOff;


}
