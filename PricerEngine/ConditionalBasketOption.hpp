#ifndef _Conditional_Basket_Option_Hpp
#define _Conditional_Basket_Option_Hpp

#include "Option.hpp"

class ConditionalBasketOption : public Option
{

public:
    ConditionalBasketOption();
    ~ConditionalBasketOption();
    ConditionalBasketOption(const nlohmann::json &json);
    double payOff(const PnlMat *matrix) override;
};

#endif
