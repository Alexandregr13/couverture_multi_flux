#ifndef _Conditional_Max_Option_Hpp
#define _Conditional_Max_Option_Hpp

#include "Option.hpp"

class ConditionalMaxOption : public Option
{

public:
    ConditionalMaxOption();
    ~ConditionalMaxOption();
    ConditionalMaxOption(const nlohmann::json &json);
    double payOff(const PnlMat *matrix) override;
};

#endif
