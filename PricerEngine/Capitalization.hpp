#pragma once

#include <cmath>
#include <functional>

using CapitalizationFunc = std::function<double(double payoff, double t_m)>;

// Crée une fonction de capitalisation pour un taux r et une maturité T
// Capitalise un flux de t_m vers T : payoff * exp(r * (T - t_m))
inline CapitalizationFunc createCapitalization(double r, double T) {
    return [r, T](double payoff, double t_m) {
        return payoff * exp(r * (T - t_m));
    };
}
