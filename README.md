# Projet de couverture de produits multi-flux


## Avertissement. Il est strictement interdit de:

- Cloner ce dépôt ou de copier son contenu sur un dépôt public.
- Transférer le code ou la documentation fournis à un outil d'IA générative pour essayer d'engendrer automatiquement une solution au projet.

  - gRPC : 1.76.0 (depuis ~/Téléchargements/grpc-install)
  - PNL : depuis /home/alexandre/3A/projet-de-couverture-de-produits-derives/pnl-1.15.0/build
  - 
  - cmake -DCMAKE_PREFIX_PATH="/path/to/pnl;/path/to/grpc" ..
  - make
  - ./pricing_server ~/3A/PCPMF/PCMF/couverture_multi_flux/TestCases/Basket_5_3_1/basket_5_3_1_math_param.json 

#### Ordinateur Jérémie:

```bash
cd ~/Documents/couverture_multi_flux/PricerEngine
rm -rf build && mkdir build && cd build

cmake -DCMAKE_BUILD_TYPE=Debug \
  -DCMAKE_PREFIX_PATH="$HOME/pnl/build;$HOME/deps/grpc-install" \
  ..

make -j

./pricing_server ../../TestCases/Basket_5_3_1/basket_5_3_1_math_param.json 
```


## Présentation

L'objectif de ce projet est d'implémenter une application permettant de couvrir des produits dérivés dans un cadre plus général que celui du Projet de Couverture de Produits Dérivés: l'application développée permettra de couvrir des produits dérivés basés sur un calendrier réel, et versant plusieurs flux.

La réalisation de ce projet permettra à chaque élève:

- De comprendre de quelle façon peut être couvert un produit dérivé multiflux.
- D'implémenter de bout en bout une application composée de deux parties:
  - Une application *financière* à laquelle sont fournies les données financières, qui gère les dates de constatation et constitue le portefeuille de couverture. Cette application prendra 3 arguments dans cet ordre
    1. le fichier de description des paramètres financiers (`financial-param.json`)
    2. le fichier de données de marché (`MarketData.csv`)
    3. le fichier contenant le portefeuille de couverture en sortie (`portfolio.json`)
  - Une application *mathématique*  dont le rôle est de calculer les quantités de sous-jacent à détenir dans le portefeuille de couverture. Cette application prendra comme unique argument le fichier des paramètres mathématiques (`math-param.json`) que l'on supposera cohérent avec son équivalent financier `financial-param.json`.

## Le modèle

On considère $`N\in \mathbb{N}`$ sous-jacents $`(S^1, \ldots, S^N)`$ modélisé de la manière suivante sous la probabilité risque neutre

### Formalisme dans le monde financier

```math
\begin{align}
  dS^{i}_t &= S^{i}_t (r dt + \sigma^{i} dB^{i}_t) \;\text{ pour }i \in \{1, \dots, N\}
\end{align}
```

où

- Les volatilités $`\sigma^i`$ pour $`i \in \{1, \dots, N\}`$ sont déterministes réelles.
- Les processus $`B^{i}`$ pour $`i \in \{1, \dots, N\}`$ sont des mouvements browniens réels corrélés. Soit la matrice $`\Gamma`$ définie par $`d\langle B^i, B^j\rangle_t = \Gamma_{i,j} dt`$ pour $`i,j \in \{1, \dots, N\}`$. Remarquons que $`\Gamma_{i,i} = 1`$.

### Formalisme dans le monde mathématique

Introduisons une matrice $`L`$ telle que $`LL^T = \Gamma`$. On peut typiquement prendre pour $`L`$ la factorisée de Cholesky de $`\Gamma`$. Introduisons pour $`i \in \{1, \dots, N\}`$, le vecteur $`\Sigma_i = \sigma^i L_i`$ où $`L_i`$ est la $`i`$-ème ligne de $`L`$.

Le modèle précédent peut être écrit sous la forme

```math
\begin{align}
  dS^{i}_t &= S^{i}_t (r dt + \Sigma_{i} \cdot dW_t) \;\text{ pour }i \in \{1, \dots, N\}
\end{align}
```

où le processus $`W = (W^1, \dots, W^N)`$ est un mouvement brownien à valeurs dans $`\mathbb{R}^N`$.  On remarquera que $`|\Sigma_i|^2 = (\sigma^i)^2`$. Le champs `volCholeskyLines` des paramètres mathématiques correspond à la matrice $`\Sigma`$.

## Payoffs

On considère $`M\in \mathbb{N}`$ dates de paiement $`(t_1,\ldots, t_M)`$. A chaque date $`t_m`$ pour $`m \in \{1,\dots, M\}`$, le produit paye un flux qui dépend des valeurs des sous-jacents jusqu'à la date courante $`m`$.

On considère deux produits particuliers

### Le panier conditionnel (`conditional_basket`)

Soit $`(K_1,\ldots, K_M)`$ $`M`$ strikes. On définit

```math
\begin{align*}
P_0 & = 0\\
P_m & = \left(\frac{1}{N} \sum_{n=1}^N S^n_{t_m} - K_m\right)_+ \, \mathbb{1}_{P_k = 0 \; \forall k < m}
\end{align*}
 ```

Le produit dérivé verse le flux $`P_m`$ à la date $`t_m`$ pour $`m \in \{1,\dots, M\}`$.

### Le call sur maximum conditionnel (`conditional_max`)

Soit $`(K_1,\ldots, K_M)`$ $`M`$ strikes. On définit

```math
\begin{align*}
P_0 & = 0\\
P_m & = \left(\max_{n \in \{1,\dots,N\}} S^n_{t_m} - K_m\right)_+ \, \mathbb{1}_{P_{m-1} = 0}
\end{align*}
 ```

Le produit dérivé verse le flux $`P_m`$ à la date $`t_m`$ pour $`m \in \{1,\dots, M\}`$.

## Organisation, contraintes techniques

- Le projet devra être réalisé en binômes.
- L'application financière devra être codée en C# (.NET 8). Il s'agira d'une application console qui prend en entrée une configuration de produit et des données de marché, et fournit en sortie un fichier json qui contient les résultats.
- Le coeur de calcul devra être codé en C++.
- La communication entre l'application financière et le coeur de calcul se fera à l'aide d'un client et d'un serveur gRPC.

```mermaid
flowchart LR
    subgraph Input
        direction LR
        md(Market data)
        fp(Financial parameters)
        mp(Mathematical parameters)
    end
    subgraph  
        direction TB
        subgraph Pricing engine
            pr(Pricer)
            mp --> pr
        end
        subgraph gr [gRPC]
            direction LR
            cl(gRPC client)
            cl <--> sr(gRPC server)
        end  
        subgraph ht [Financial app]
        md --> hd(Hedging engine)
        hd -->pf(Portfolio)
        fp --> hd
        end  
    end
    subgraph out [Output]
    pf --> cs(json files)
    end
    hd <--> cl
    sr <--> pr
```

## Jeux de données

Des jeux de données de marché et des fichiers de paramètres sont disponibles pour les deux payoffs dans le répertoire [TestCases/](TestCases).

## Etapes principales du projet

- Faire valider l'architecture et le principe mathématique de couverture
- Réussir à faire communiquer le client et le serveur grpc (squelettes)
- Implémentation d'un outil qui couvre un call vanille
- Implémentation d'un outil qui couvre les deux produits décrits au dessus.

## Moteur de calcul

### Sur les machines de l'Ensimag sous Linux

- La librairie [PNL](https://pnlnum.github.io/pnl) est déjà installée :
  - version optimisée: `/matieres/5MMPCPD/pnl/`
  - version avec symboles de débugage: `/matieres/5MMPCPD/pnl-dbg/`
- La librairie [nlohmann_json](https://github.com/nlohmann/) de manipulation de fichiers `.json` est déjà installée dans les répertoires système.
- Les librairies `grpc` et `protobuf` sont installées dans `/matieres/5MMPCMF/grpc`
- Pour compiler le code C++ fourni

  ```
  cd PricerEngine
  mkdir build
  cd build
  cmake -DCMAKE_PREFIX_PATH="/matieres/5MMPCPD/pnl;/matieres/5MMPCMF/grpc" ..
  ```

### Installation sous Linux et Mac

Le moteur de calcul codé en C++ utilisera les outils/libraires suivantes

- `CMake`
- Une version récente de `g++` ou `clang++`.
- [PNL](https://pnlnum.github.io/pnl) : installer la dernière version à partir de https://github.com/pnlnum/pnl/releases. Sous Ubuntu, il faut préalablement installer `libblas-dev`, `liblapack-dev` et `gfortran`.
- Une libraire de manipulation de fichiers `.json` en C++ : [nlohmann_json](https://github.com/nlohmann/). Cette librairie est disponible dans la plupart des gestionnaires de paquets
  - Homebrew : `brew install nlohmann-json`
  - Ubuntu : installer `nlohmann-json3-dev`

  Voir un exemple d'utilisation dans [`PricerEngine/test_json_reader.cpp`](PricerEngine/test_json_reader.cpp).
- Installer `grpc` et `protobuf`
  - Homebrew : `brew install grpc`
  - Ubuntu : installer le paquet `protobuf-compiler`. Le paquet `libgrpc++-dev` ne contient pas les fichiers permettant la détection de `grpc` par `CMake`. vous devez donc installer `grpc` depuis https://grpc.io/docs/languages/cpp/quickstart.

### Installation sous Windows

- **Prérequis**: avoir installé CMake (de préférence Cmake gui) et git sur la machine.
- Télécharger et installer *vcpkg* depuis github en suivant les [instructions fournies](https://github.com/microsoft/vcpkg#quick-start-windows).
- Se servir de *vcpkg* pour installer Protobuf, gRPC et nlohmann-json en version 64 bits:
  - .\vcpkg.exe install grpc:x64-windows
  - .\vcpkg.exe install protobuf:x64-windows
  - .\vcpkg.exe install nlohmann-json:x64-windows

 Dans Cmake-gui, ajouter les deux entrées suivantes:
  - CMAKE_PREFIX_PATH: chemin vers le dossier de libpnl qui contient les répertoires *lib* et *include*
  - CMAKE_TOOLCHAIN_FILE: chemin vers le fichier *[racine vcpkg]/scripts/buildsystems/vcpkg.cmake*

### FAQ

- **Erreur de segmentation dans le serveur GPRC C+`**:
  - Si vous rencontrez une erreur de segmentation à la déserialisation des données par `protobuf`, le problème vient peut-être d'une incompatibilité entre le compilateur utilisé pour compiler votre projet et celui qui a servi à compiler la librairie `protobuf`. Dans ce cas, recompiler `protobuf` à la main.
  - Avec `homebrew`: `brew reinstall --build-from-source protobuf`
  - Référence : https://github.com/protocolbuffers/protobuf/issues/21447
- **Packages nécessaires pour une compilation sous Ubuntu** :
  - Installer `protobuf-compiler-grpc`, `libgrpc-dev` et `libgrpc++-dev`.
- **SSL is not found on OSX** :
  - Installer `pkg-config`: `brew install pkg-config``
  - Exécuter `export PKG_CONFIG_PATH="$(brew --prefix)/opt/openssl@1.1/lib/pkgconfig"` ou lancer `cmake` avec la commande `PKG_CONFIG_PATH="$(brew --prefix)/opt/openssl@1.1/lib/pkgconfig" cmake`.
- **Intellisense does not find `pricing.grpc.pb.h`** : ajouter le répertoire de `build` à la liste des répertoires utilisés par intellisense. Sous VS Code, ajouter `${workspaceFolder}/PricerEngine/build` à `includePath`.
- **Erreur de compilation à cause de *Status::OK*:** ceci est dû à un conflit dans les noms de constantes. Utiliser une version de PNL `>=1.11.0`.
- **Ubuntu: `cmake` ne trouve pas`gRPCConfig.cmake`** : https://bugs.launchpad.net/ubuntu/+source/grpc/+bug/1935709. Il faut utiliser la version source de `grpc`: https://github.com/grpc/grpc.
- **Nuget ne trouve pas le package alors que la source est disponible**: Nuget peut arrêter son exécution et indiquer ne pas avoir trouvé un package une source de package est erronée (par exemple quand un chemin vers un fichier est spécifié dans les sources, au lieu d'un chemin vers un répertoire). Les sources de package peuvent être inspectées depuis Visual Studio (Windows), ou bien via la commande `dotnet nuget list source`. Si une source est erronée, elle peut être supprimée à l'aide de la commande `dotnet nuget remove source [nom de la source]`. **Attention:** il faut spécifier le *nom* de la source, et pas son chemin dans la commande précédente.
- **Erreur de compilation gRPC après un merge**: il est possible qu'il y ait des différences d'encodage du fichier Protobuf (*pricing.proto* ici). Ceci peut être détecté dans la fenêtre *Sortie* à la compilation. Il faut alors récupérer une nouvelle version du fichier pour rétablir un encodage qui convient.
- **Erreurs de parsing du JSon avec *nlohmann* à l'exécution de *pricing_server***:
  - S'assurer que le chemin vers le fichier JSon est un chemin absolu
  - S'assurer que le fichier JSon est encodé en UTF-8
- **Comment créer un Client .NET invoquant un serveur via *http* au lieu de *https***: configurer le client (*GrpcPricer.GrpcPricerClient* dans le code ci-dessous) de la façon suivante:

  ```csharp
  var httpHandler = new HttpClientHandler();
  // Return `true` to allow certificates that are untrusted/invalid
  httpHandler.ServerCertificateCustomValidationCallback =
      HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;
  using var channel = GrpcChannel.ForAddress("http://localhost:5079",
      new GrpcChannelOptions { HttpHandler = httpHandler });
  var client = new GrpcPricer.GrpcPricerClient(channel);
   ```

- **Comment inspecter le contenu d'un `PnlVect` ou `PnlMat` depuis la fenêtre espion (*Watch*) de Visual Studio** ou **Visual Studio Code**:
  - Pour lire le contenu de l'emplacement `i` d'un `PnlVect* my_vect`, utiliser `my_vect->array[i]`.
  - Pour lire l'intégralité d'un `PnlVec* my_vect` taille `n`, utiliser l'une des syntaxes `*(double(*)[n])my_vect->array`, `*(my_vect->array)@n`, `(my_vect->array),n`
  - Pour lire l'intégralité de la ligne `i` d'une matrice `PnlMat *my_mat` de taille `m x n`, utiliser `*(double(*)[n])(my_mat->array + i * n`, `*(my_mat->array + i * n)@n`, `(my_mat->array + i * n),n`.
  - Voir la discussion https://github.com/Microsoft/vscode-cpptools/issues/172 pour plus d'information.

## Package Nuget *MultiCashFlow.Common* et sa documentation

Le package est disponible à cette adresse:

- [version 1.0.2](https://www.dropbox.com/scl/fi/03uouwip74z19reanuo2u/MultiCashFlow.Common.1.0.2.nupkg?rlkey=4c7po2ssruy51vaw5t4zrqhg4&dl=0)

La documentation correspondante est disponible [ici](https://www.dropbox.com/scl/fi/ki64lg1jjs9swgzr3lrw0/PricingLibraryDoc.chm?rlkey=caxcnx0fxzf356wiiro7isaom&dl=0)

### Installation sous Linux

- Mettre à jour les sources de packages pour inclure le dossier contenant le package *MultiCashFlow.Common* à l'aide de la commande *`dotnet nuget add source [chemin vers le dossier]`*
- Installer le package dans le projet voulu à l'aide de la commande *`dotnet add [NomProjet.csproj] package MultiCashFlow.Common`*

## Autres ressources

- Présentation des principes [SOLID](https://www.dropbox.com/scl/fi/k848zepp2hmfw9sxbr1mg/PresentationSolid.pdf?rlkey=nrex5ernp6n0f2gkxfbkherue&dl=0)
- [Code source](https://www.dropbox.com/scl/fi/maugznmqxosambdh9h2xt/barrier_option_pricing.zip?rlkey=l6hzw6h34c1fymkavfsw80sui&dl=0) du pricer d'options barrières.

## Modalités de rendu

**Date de rendu** : 20/12/2024 à 17h

**Site de dépôt** : TEIDE

**Cas tests d'évaluation** : les résultats à inclure dans l'archive correspondent aux cas tests du répertoire [`EvalCases/`](EvalCases/). Merci de respecter le nommage suivant pour les fichiers contenant les portefeuille de couverture

- cas test `Basket_1` :résultats dans `portfolio1.json`
- cas test `Basket_2` : résultats dans `portfolio2.json`
- cas test `Max_3` : résultats dans `portfolio3.json`
- cas test `Max_4` : résultats dans `portfolio4.json`

Vous devez rendre une archive `Equipe_i.zip` où `i` correspond au numéro de l'équipe sur TEIDE. Cette archive créera à l'extraction l'arborescence suivante

```
Equipe_i/
    |- src
    |- AUTHORS
    |– output/
        |- portfolio1.json
        |- portfolio2.json
        |- portfolio3.json
        |- portfolio4.json
```

où

- `src` contient l'ensemble du code source (tous les projets `C++` ou `C#`)
- `AUTHORS` est un fichier texte contenant les prénoms, noms et mail grenoble-inp de chaque membre de l'équipe.
- `output` contient les fichiers `portfolio1.json`, `portfolio2.json`,... qui contiennent chacun la chaîne de caractères json qui aura été générée par le package `Nuget`.

