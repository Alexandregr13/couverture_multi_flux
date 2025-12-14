cmake_minimum_required(VERSION 3.15)

project(TestPricing C CXX)

set(CMAKE_CXX_STANDARD 17)

# Cmake modules
list(APPEND CMAKE_MODULE_PATH "${CMAKE_SOURCE_DIR}/cmake")
set(PRICER_ENGINE_DIR "${CMAKE_SOURCE_DIR}/../../PricerEngine")

# Set PNL_ROOT
set(PNL_ROOT "/home/alexandre/3A/projet-de-couverture-de-produits-derives/pnl-1.15.0/build")

# Find Pnl
find_package(Pnl REQUIRED)
set(LIBS ${LIBS} ${PNL_LIBRARIES})
include_directories(${PNL_INCLUDE_DIRS})

# Find nlohmann_json
find_package(nlohmann_json REQUIRED)

# Include PricerEngine headers
include_directories(${PRICER_ENGINE_DIR})

# Source files from PricerEngine
set(PRICER_SOURCES
    ${PRICER_ENGINE_DIR}/pricer.cpp
    ${PRICER_ENGINE_DIR}/BlackScholesModel.cpp
    ${PRICER_ENGINE_DIR}/Option.cpp
    ${PRICER_ENGINE_DIR}/ConditionalBasketOption.cpp
    ${PRICER_ENGINE_DIR}/ConditionalMaxOption.cpp
    ${PRICER_ENGINE_DIR}/json_reader.cpp
)

add_executable(test_pricing test_pricing.cpp ${PRICER_SOURCES})

target_link_libraries(test_pricing
    ${PNL_LIBRARIES}
    nlohmann_json::nlohmann_json
)
