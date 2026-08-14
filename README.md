# iRacing Setup Manager

Application Windows en français destinée à centraliser, classer, vérifier et copier
des setups iRacing sans modifier les fichiers originaux.

> Le projet utilise uniquement les fichiers placés dans les dossiers locaux
> autorisés par l'utilisateur. Il ne contourne aucune authentification,
> limitation, protection ou condition d'abonnement et n'effectue aucun upload
> direct vers Garage61.

## État du projet

La version actuelle est `1.2.8.27`. Elle comprend la bibliothèque locale, la
surveillance des dossiers autorisés, la validation, la copie vers iRacing et
iRacing Team, les sauvegardes et les mises à jour automatiques.

## Technologies

- C# et .NET 10 LTS
- WinUI 3 / Windows App SDK 2.3.1
- interface WinUI en C# avec pages XAML
- SQLite avec Entity Framework Core
- xUnit pour les tests
- GitHub Actions pour l'intégration continue

Les fournisseurs, catégories et voitures prises en charge sont définis dans un
catalogue unique. Les alias de circuits sont partagés entre l'analyse des noms de
fichiers et l'import du catalogue local iRacing.

## Organisation

```text
src/
  IracingSetupManager.App/              Interface Windows WinUI 3
  IracingSetupManager.Core/             Modèles et règles métier
  IracingSetupManager.Infrastructure/   Base, fichiers, journaux et sécurité
  IracingSetupManager.Integrations/     Vérification et installation des mises à jour
tests/
  IracingSetupManager.Core.Tests/       Tests automatisés
docs/                                   Architecture et décisions
```

## Prérequis utilisateur

Pour installer et utiliser une version publiée :

- Windows 10 version 1809 ou Windows 11 ;
- l'installateur iRacing Setup Manager.

Visual Studio et le SDK .NET ne sont pas nécessaires. Les versions publiées seront
autonomes et embarqueront .NET ainsi que les composants Windows App SDK requis.

## Prérequis de développement

- Windows 10 version 1809 ou Windows 11
- Visual Studio 2026 avec la charge de travail de développement d'applications
  Windows et les outils WinUI
- SDK .NET 10

## Compilation

```powershell
dotnet restore IracingSetupManager.sln
dotnet build IracingSetupManager.sln --configuration Release --maxcpucount:1
```

## Tests

```powershell
dotnet test IracingSetupManager.sln --configuration Release --maxcpucount:1
```

## Sécurité

Les setups à vérifier peuvent être corrigés manuellement. Une abréviation de
voiture ou de circuit peut être mémorisée dans le dictionnaire local afin que les
prochains fichiers similaires soient reconnus automatiquement. Ces règles restent
sur l'ordinateur de l'utilisateur et peuvent être supprimées dans Paramètres.

Ne jamais ajouter au dépôt de mot de passe, cookie, jeton, archive personnelle,
base locale ou certificat. L'application ne demande actuellement aucun identifiant
de fournisseur. Les secrets de publication doivent être enregistrés dans GitHub
Actions Secrets.

## Méthode de contribution

1. Créer une branche depuis `main`, par exemple `feature/library-import`.
2. Réaliser une modification ciblée et ajouter ses tests.
3. Vérifier la compilation et les tests.
4. Ouvrir une pull request vers `main`.
5. Utiliser des messages de commit courts et explicites en français.

## Publication

Les versions suivent le format `MAJEURE.MINEURE.CORRECTIF` avec une quatrième
partie facultative. La création d'un tag déclenche les tests, la compilation, la génération de l'installateur,
le calcul SHA-256 et la publication dans GitHub Releases.

Les livrables Windows seront publiés en mode autonome. Exemple pour Windows x64 :

```powershell
dotnet publish src/IracingSetupManager.App/IracingSetupManager.App.csproj `
  --configuration Release `
  --runtime win-x64 `
  --self-contained true `
  --maxcpucount:1
```

### Installateur Windows

La commande `installer/Build-Installer.ps1 -Version 1.2.8.27` publie l’application
autonome, génère l’installateur Inno Setup versionné et son empreinte SHA-256. Le
même identifiant d’application assure installation, désinstallation et mise à
niveau. L’installateur ne supprime jamais `%LocalAppData%\IracingSetupManager`,
afin de conserver la base SQLite et les réglages. Si un certificat valide et
SignTool sont disponibles, les exécutables sont signés automatiquement.

### Publication des mises à jour

Un tag Git `vX.Y.Z` ou `vX.Y.Z.R` déclenche `.github/workflows/release.yml`. Le workflow teste
l’application, construit l’installateur et publie automatiquement l’exécutable et
son fichier SHA-256 dans GitHub Releases. Les secrets facultatifs
`SIGNING_CERTIFICATE_BASE64` et `SIGNING_CERTIFICATE_PASSWORD` activent la
signature. L’application ne propose jamais une release privée d’un des deux assets.

## Fiabilité et base locale

Les actions sensibles de l'interface interceptent les erreurs attendues et les
écrivent dans des journaux quotidiens expurgés sous les données locales de
l'application. Le schéma SQLite est versionné dans `SchemaMigrations` : chaque
évolution est appliquée une seule fois et testée sur une ancienne structure.

La suite automatisée couvre aussi la logique de recherche et de filtrage utilisée
par les pages Bibliothèque et À vérifier. Les contrôles WinUI eux-mêmes restent à
valider visuellement sur Windows lors de chaque publication.

## Licence

Tous droits réservés. Voir [LICENSE](LICENSE).
