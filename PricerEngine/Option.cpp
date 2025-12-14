#include "Option.hpp"

#include "string.h"
#include "ConditionalBasketOption.hpp"
#include "ConditionalMaxOption.hpp"
#include <iostream>

using namespace std;

Option::Option()
{
}

Option::Option(const nlohmann::json &json)
{
    json.at("Strikes").get_to(strike);
    json.at("MathPaymentDates").get_to(paymentDate);
    json.at("DomesticInterestRate").get_to(intersertRate);

}
Option::~Option()
{
    pnl_vect_free(&strike);
    pnl_vect_free(&paymentDate);
}


Option *instance_option(const nlohmann::json &json)
{
    Option *opt = NULL;
    string optionType;
    json.at("PayoffType").get_to(optionType);

    if (optionType == "ConditionalBasket")
        opt = new ConditionalBasketOption(json);
    else if (optionType == "ConditionalMax")
        opt = new ConditionalMaxOption(json);
    else
    {
        cout << "Option " << optionType << " unknow. Abort." << endl;
        abort();
    }

    return opt;
}
