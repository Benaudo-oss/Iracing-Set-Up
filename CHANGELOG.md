# Journal des modifications

Les changements importants de ce projet sont consignés dans ce fichier. Le projet
utilise le versionnement sémantique.

## [Non publié]

### Ajouté

- Publication GitHub Actions complète et relançable : compilation, tests, installateur, SHA-256 et notes automatiques.
- Vérification GitHub Releases, téléchargement avec SHA-256, report/ignorance, installation séparée et retour arrière.
- Installateur Windows 0.1.0 versionné, compatible mise à niveau et signature facultative, avec conservation des données locales.
- Suite de tests étendue : classement, empreintes, reprise, fournisseurs simulés, iRacing et Garage61 simulé.
- Coffre-fort Windows pour les secrets, journaux expurgés, chemins et ZIP durcis, réduction des données sensibles et sauvegarde SQLite.
- Aperçu et copie sécurisée des setups validés vers iRacing, avec résolution explicite des conflits.
- Structure initiale de la solution .NET 10 et WinUI 3.
- Séparation des modules métier, infrastructure, fournisseurs et intégrations.
- Protections Git contre l'ajout de secrets et de données locales.
- Projet de tests automatisés.
- Schéma SQLite complet pour les setups et les résultats Garage61.
- Choix persistant du dossier d'archive, demandé uniquement la première fois.
- Surveillance limitée aux téléchargements et applications officielles.
- Interdiction explicite de surveiller le dossier iRacing et protection des setups privés.
- Import de bibliothèques existantes, extraction ZIP sécurisée et classement sans renommage.
- Détection SHA-256 et conservation des collisions sans écrasement.
- Interface Windows sombre en français avec navigation, filtres et confirmations.
- Surveillance persistante avec attente de fin de téléchargement et balayage de reprise.
- Validation et refus individuels ou groupés avec confirmation obligatoire.
- Notes, commentaires et historique détaillé de chaque changement.
