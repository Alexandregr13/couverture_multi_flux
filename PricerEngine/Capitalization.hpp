#pragma once

#include <cmath>
#include <functional>

using CapitalizationFunc = std::function<double(double payoff, double t_m)>;

// Capitalise le flux de t_m vers T 
inline CapitalizationFunc createCapitalization(double r, double T) {
    return [r, T](double payoff, double t_m) {
        return payoff * exp(r * (T - t_m));
    };
}
