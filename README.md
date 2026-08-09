# iRacing Setup Manager

Application Windows en français destinée à centraliser, classer, vérifier et copier
des setups iRacing sans modifier les fichiers originaux.

> Le projet n'utilisera que les API et méthodes d'importation officiellement
> autorisées par les fournisseurs et Garage61. Il ne contournera aucune
> authentification, limitation, protection ou condition d'abonnement.

## État du projet

Le projet est en phase d'initialisation. La version actuelle est `0.1.4.2` :
bibliothèque locale, calcul SHA-256, base SQLite et interface de consultation.

## Technologies

- C# et .NET 10 LTS
- WinUI 3 / Windows App SDK 2.3.1
- MVVM avec CommunityToolkit.Mvvm
- SQLite avec Entity Framework Core
- xUnit pour les tests
- GitHub Actions pour l'intégration continue

## Organisation

```text
src/
  IracingSetupManager.App/              Interface Windows WinUI 3
  IracingSetupManager.Core/             Modèles et règles métier
  IracingSetupManager.Infrastructure/   Base, fichiers, journaux et sécurité
  IracingSetupManager.Providers/        HYMO, GO, GNG et orchestration parallèle
  IracingSetupManager.Integrations/     iRacing, Garage61 et mises à jour
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

Ne jamais ajouter au dépôt de mot de passe, cookie, jeton, archive personnelle,
base locale ou certificat. Les secrets de développement doivent rester dans le
gestionnaire d'informations d'identification Windows. Les secrets de publication
doivent être enregistrés dans GitHub Actions Secrets.

## Méthode de contribution

1. Créer une branche depuis `main`, par exemple `feature/library-import`.
2. Réaliser une modification ciblée et ajouter ses tests.
3. Vérifier la compilation et les tests.
4. Ouvrir une pull request vers `main`.
5. Utiliser des messages de commit courts et explicites en français.

## Publication

Les versions suivront le format `MAJEURE.MINEURE.CORRECTIF`. À terme, la création
d'un tag déclenchera les tests, la compilation, la génération de l'installateur,
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

La commande `installer/Build-Installer.ps1 -Version 0.1.4.2` publie l’application
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

## Licence

Tous droits réservés. Voir [LICENSE](LICENSE).
