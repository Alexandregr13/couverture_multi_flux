#include <iostream>
#include <fstream>
#include <sstream>
#include <cmath>
#include <string>
#include <vector>
#include <map>
#include <nlohmann/json.hpp>
#include "pnl/pnl_vector.h"
#include "pnl/pnl_matrix.h"
#include "pricer.hpp"

using namespace std;

void printResults(double price, double priceStdDev, PnlVect* deltas, PnlVect* deltasStdDev) {
    cout << "Price: " << price << " (+/- " << priceStdDev << ")" << endl;
    cout << "Deltas: ";
    pnl_vect_print_asrow(deltas);
    cout << "Deltas StdDev: ";
    pnl_vect_print_asrow(deltasStdDev);
}

void compareWithExpected(double price, double expectedPrice, PnlVect* deltas, const vector<double>& expectedDeltas,
                         double priceStdDev, const vector<double>& expectedDeltasStdDev) {
    double priceError = fabs(price - expectedPrice);
    double priceRelError = (expectedPrice > 0) ? priceError / expectedPrice * 100 : 0;
    cout << "\n--- Comparison ---" << endl;
    cout << "Price: " << price << " vs Expected: " << expectedPrice;
    cout << " (error: " << priceError << ", " << priceRelError << "%)" << endl;

    bool priceOk = priceError < 3 * priceStdDev || priceRelError < 5;
    cout << "Price check: " << (priceOk ? "PASS" : "FAIL") << endl;

    cout << "Delta errors: ";
    bool deltasOk = true;
    for (int i = 0; i < deltas->size && i < (int)expectedDeltas.size(); i++) {
        double deltaError = fabs(pnl_vect_get(deltas, i) - expectedDeltas[i]);
        double expectedStd = (i < (int)expectedDeltasStdDev.size()) ? expectedDeltasStdDev[i] : 0.01;
        cout << deltaError << " ";
        if (deltaError > 3 * expectedStd && deltaError > 0.01) {
            deltasOk = false;
        }
    }
    cout << endl;
    cout << "Deltas check: " << (deltasOk ? "PASS" : "FAIL") << endl;
}

vector<double> readInitialSpots(const string& mktDataPath, int nAssets) {
    vector<double> spots(nAssets, 100.0);

    ifstream file(mktDataPath);
    if (!file.is_open()) {
        cerr << "Warning: Could not open market data file: " << mktDataPath << endl;
        return spots;
    }

    string line;
    getline(file, line); // Skip header

    map<int, double> assetSpots;
    string firstDate = "";

    while (getline(file, line)) {
        stringstream ss(line);
        string id, date, value;

        getline(ss, id, ',');
        getline(ss, date, ',');
        getline(ss, value, ',');

        if (firstDate.empty()) {
            firstDate = date;
        }

        if (date != firstDate) {
            break; // Only read first date
        }

        // Extract asset index from id (e.g., "id_0" -> 0)
        size_t pos = id.find('_');
        if (pos != string::npos) {
            int idx = stoi(id.substr(pos + 1));
            if (idx < nAssets) {
                assetSpots[idx] = stod(value);
            }
        }
    }

    for (auto& p : assetSpots) {
        if (p.first < nAssets) {
            spots[p.first] = p.second;
        }
    }

    return spots;
}

int main(int argc, char** argv) {
    if (argc < 2) {
        cout << "Usage: " << argv[0] << " <math_param.json> [portfolio.json] [mkt_data.csv]" << endl;
        return 1;
    }

    string mathParamPath = argv[1];
    string portfolioPath = (argc > 2) ? argv[2] : "";
    string mktDataPath = (argc > 3) ? argv[3] : "";

    // Load math parameters
    ifstream mathFile(mathParamPath);
    if (!mathFile.is_open()) {
        cerr << "Cannot open " << mathParamPath << endl;
        return 1;
    }
    nlohmann::json jsonParams = nlohmann::json::parse(mathFile);
    mathFile.close();

    // Create pricer
    BlackScholesPricer pricer(jsonParams);
    pricer.print();

    // Get initial spots
    int nAssets = pricer.nAssets;
    PnlMat* past = pnl_mat_create(1, nAssets);

    vector<double> expectedDeltas;
    vector<double> expectedDeltasStdDev;
    double expectedPrice = 0.0;
    double expectedPriceStdDev = 0.0;

    // Try to read initial spots from market data
    vector<double> spots;
    if (!mktDataPath.empty()) {
        spots = readInitialSpots(mktDataPath, nAssets);
    } else {
        spots.resize(nAssets, 100.0);
    }

    for (int i = 0; i < nAssets; i++) {
        pnl_mat_set(past, 0, i, spots[i]);
    }

    // Load expected values from portfolio
    if (!portfolioPath.empty()) {
        try {
            ifstream portfolioFile(portfolioPath);
            if (!portfolioFile.is_open()) {
                cerr << "Warning: cannot open portfolio file: " << portfolioPath << endl;
            } else {
                nlohmann::json portfolio = nlohmann::json::parse(portfolioFile);

                if (portfolio.is_array() && !portfolio.empty()) {
                    const auto& firstEntry = portfolio[0];

                    // Handle both naming conventions
                    const char* PRICE_KEY  = firstEntry.contains("Price")  ? "Price"  : "price";
                    const char* DELTA_KEY  = firstEntry.contains("Delta")  ? "Delta"  : "deltas";
                    const char* PSTD_KEY   = firstEntry.contains("PriceStdDev") ? "PriceStdDev" : "priceStdDev";
                    const char* DSTD_KEY   = firstEntry.contains("DeltaStdDev") ? "DeltaStdDev" : "deltasStdDev";

                    if (firstEntry.contains(PRICE_KEY)) {
                        expectedPrice = firstEntry.at(PRICE_KEY).get<double>();
                    }
                    if (firstEntry.contains(DELTA_KEY)) {
                        expectedDeltas = firstEntry.at(DELTA_KEY).get<vector<double>>();
                    }
                    if (firstEntry.contains(PSTD_KEY)) {
                        expectedPriceStdDev = firstEntry.at(PSTD_KEY).get<double>();
                    }
                    if (firstEntry.contains(DSTD_KEY)) {
                        expectedDeltasStdDev = firstEntry.at(DSTD_KEY).get<vector<double>>();
                    }
                } else {
                    cerr << "Warning: portfolio file is not a non-empty array." << endl;
                }
            }
        } catch (const std::exception& e) {
            cerr << "Error while reading portfolio.json: " << e.what() << endl;
        }
    }


    cout << "\n=== Testing at t=0 ===" << endl;
    cout << "Initial spots: ";
    for (int i = 0; i < nAssets; i++) {
        cout << pnl_mat_get(past, 0, i) << " ";
    }
    cout << endl;

    // Price and deltas at t=0
    double price, priceStdDev;
    PnlVect *deltas, *deltasStdDev;

    pricer.priceAndDeltas(past, 0.0, true, price, priceStdDev, deltas, deltasStdDev);

    printResults(price, priceStdDev, deltas, deltasStdDev);

    if (expectedPrice > 0) {
        compareWithExpected(price, expectedPrice, deltas, expectedDeltas, priceStdDev, expectedDeltasStdDev);
    }

    // Test at a time between dates (t=0.1)
    cout << "\n=== Testing at t=0.1 (between dates) ===" << endl;
    PnlVect *deltas2, *deltasStdDev2;
    double price2, priceStdDev2;
    pricer.priceAndDeltas(past, 0.1, false, price2, priceStdDev2, deltas2, deltasStdDev2);
    printResults(price2, priceStdDev2, deltas2, deltasStdDev2);

    // Cleanup
    pnl_mat_free(&past);
    pnl_vect_free(&deltas);
    pnl_vect_free(&deltasStdDev);
    pnl_vect_free(&deltas2);
    pnl_vect_free(&deltasStdDev2);

    cout << "\n=== Test completed ===" << endl;
    return 0;
}
