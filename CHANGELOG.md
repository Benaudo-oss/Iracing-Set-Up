# Journal des modifications

Les changements importants de ce projet sont consignés dans ce fichier. Le projet
utilise le versionnement sémantique.

## [Non publié]

### Ajouté

- Correction du démarrage WinUI, du tableau de bord SQLite et ajout d’un journal de diagnostic expurgé.
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
# 0.1.1.5

- Classement automatique des exemples GO, VRS, HYMO et Grid & Go fournis.
- Reconnaissance des saisons courtes comme `26S3`, des voitures, circuits et variantes Race, Safe et Wet Race.
- Méthode de secours publique lorsque l’API GitHub atteint sa limite de requêtes.

# 0.1.1.3

- Ajout d’un bouton pour effacer la recherche et tous les filtres de la bibliothèque.

# 0.1.1.2

- Ajout de VRS comme quatrième fournisseur indépendant.
- Ajout du dossier surveillé VRS et de sa détection dans les noms de fichiers.
- Alignement des cases de sélection et des colonnes de la bibliothèque.

# 0.1.1.1

- Correction du crash lors du changement de menu après le lancement de la synchronisation.
- Tri compatible SQLite des setups à vérifier, de la bibliothèque et de l’historique.

# 0.1.1

- Ajout du logo voiture et engrenage dans l’application et l’installateur.
- Amélioration du contraste et de la lisibilité du menu de navigation.
- Nouvelle version destinée à valider le mécanisme de mise à jour.
