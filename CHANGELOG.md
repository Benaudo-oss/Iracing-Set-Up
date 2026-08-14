# Journal des modifications

Les changements importants de ce projet sont consignés dans ce fichier. Le projet
utilise le versionnement sémantique.

## [Non publié]

# 1.2.9.31

- Reconnaissance des marqueurs de catégorie collés au nom de la voiture.
- Prise en charge généralisée de GT3, GT4, GTE, LMP2, LMP3, GTP et PCUP.
- Ajout de l’alias compact VantageGT4 pour l’Aston Martin Vantage GT4.

# 1.2.9.30

- Application stricte du filtre de catégories pendant les synchronisations manuelles et automatiques.
- Les catégories décochées ou non identifiées ne sont plus importées.
- Ajout de tests couvrant les catégories inconnues et l’absence de sélection.

# 1.2.9.29

- Remplacement des sélecteurs personnalisés par des cases à cocher Windows fiables.
- Conservation de la présentation en cartes et de la logique de filtrage des fournisseurs et catégories.

# 1.2.9.28

- Rétablissement d’un témoin de sélection visible pour chaque fournisseur et chaque catégorie.
- Affichage d’un cercle vide pour les options désélectionnées et d’une coche bleue pour les options actives.

# 1.2.9.27

- Modernisation du tableau de bord avec cartes, graphiques et chronologie.
- Uniformisation des filtres et ajout de pastilles supprimables.
- Amélioration visuelle des tableaux, statuts et états vides.
- Nouvelle présentation des fournisseurs et catégories dans la synchronisation.
- Sélection rapide de tous les fournisseurs ou de toutes les catégories.

# 1.2.8.27

- Conserve le raccourci du Bureau existant pendant une mise à jour afin de ne
  plus réinitialiser sa position, son apparence ou les choix de l'utilisateur.
- Utilise explicitement l'icône intégrée à l'application lors de la création
  d'un nouveau raccourci.

# 1.2.8.26

- Affiche la progression réelle et le volume téléchargé pendant une mise à jour.
- Traduit les limites GitHub, délais et coupures réseau en messages compréhensibles.
- Mémorise la version attendue avant installation et confirme automatiquement le
  succès ou l’échec lors du redémarrage de l’application.

- Transforme la synchronisation en suivi temps réel avec progression par fichier,
  récapitulatif, arrêt propre et protection contre les lancements simultanés.
- Conserve deux modes explicites : manuel, ou automatique uniquement lorsque
  l'utilisateur l'active dans Paramètres ; ce choix reste désactivé par défaut.
- Rend les copies d'archive atomiques afin qu'une interruption ne laisse aucun
  fichier final incomplet.

- Ajoute la correction manuelle des métadonnées depuis l’onglet À vérifier,
  avec reclassement sécurisé du fichier archivé et historique du changement.
- Permet de mémoriser des abréviations personnelles de voitures et circuits,
  puis de les consulter ou supprimer depuis les paramètres.
- Ajoute une migration SQLite pour le dictionnaire de reconnaissance et des
  tests couvrant l’apprentissage, les doublons et le reclassement.

- Retire les connecteurs de téléchargement direct fournisseurs non raccordés et
  conserve uniquement l'import par surveillance des dossiers autorisés.
- Retire les fondations d'upload direct Garage61, leur compteur et leurs champs
  SQLite, avec migration automatique des anciennes bases sans perte de setup.
- Supprime CommunityToolkit.Mvvm, l'ancienne interface de copie iRacing et la
  méthode de surveillance qui n'étaient pas utilisées.
- Centralise les fournisseurs, catégories, voitures actuelles et codes de dossiers
  Team, puis partage la résolution des circuits avec le catalogue iRacing local.
- Versionne les migrations SQLite et ajoute des tests de migration et de logique
  d'interface indépendants de WinUI.
- Sécurise les opérations principales de l'interface, active la journalisation
  quotidienne et retire les réglages et indicateurs qui n'avaient aucun effet.
- Retire le modèle métier et le coffre de secrets devenus sans consommateurs,
  ainsi que les anciens statuts et noms XAML inutilisés.

# 1.2.7.26

- Utilise le format `week_X` pour les dossiers de semaine de la copie iRacing Team.

# 0.2.7.25

- Normalise les dossiers fournisseurs de la copie iRacing Team avec les codes GNG, GO, HYMO, SRS, VRS et CDA.

# 0.2.7.24

- Reconnaît `ARX` comme alias de l’Acura ARX-06 GTP.

# 0.2.7.23

- Ajoute la copie vers iRacing Team avec un nom de Team Garage61 persistant et un historique indépendant.
- Ajoute au tableau de bord les copies Team et les sept fournisseurs réellement disponibles.
- Affiche les dix dernières activités en boucle et enregistre les imports et copies iRacing/Team.
- Ajoute un bouton sécurisé pour effacer uniquement l’historique de l’application.

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
# 0.1.7.18

- Reconnaît `RBR` comme abréviation exacte du Red Bull Ring.
- Conserve également les variantes `RBRing`, `RedBullRing`, `Spielberg` et `A1Ring`.

# 0.1.7.17

- Rétablit les filtres de l’onglet « À vérifier ».
- Ajoute la recherche et les filtres fournisseur, catégorie, voiture et circuit.
- Conserve les filtres pendant l’ajout incrémental des nouvelles lignes.

# 0.1.7.5

- Actualise les tableaux ligne par ligne sans réinitialiser la page, les filtres ou le défilement.
- Étend le catalogue des circuits et accepte les abréviations, noms collés et fautes limitées.
- Reconnaît Donington National via `NTL`, ainsi que les variantes `Donington` et `Donnington`.
- Distingue les Corvette Z06 GT3.R, C8.R GTE, C6.R et C7 Daytona Prototype.

# 0.1.6.5

- Applique réellement les fournisseurs et catégories sélectionnés à la synchronisation.
- Enregistre la sélection avant un import manuel ou le démarrage de la surveillance.
- Filtre chaque setup contenu dans les archives ZIP et RAR avant son archivage.

# 0.1.5.5

- Désactive la surveillance automatique par défaut.
- Ignore l’ancienne valeur activée implicitement et exige un nouveau choix volontaire de l’utilisateur.
- Conserve ensuite normalement le choix explicite enregistré dans les paramètres.

# 0.1.5.4

- Ajoute SRS comme fournisseur indépendant, configurable et surveillable séparément.
- Ajoute les alias `M8` pour BMW M8 GTE et `Caddy` pour Cadillac V-Series.R.
- Reconnaît Mosport comme Canadian Tire Motorsport Park.
- Mémorise les fournisseurs et catégories cochés dans la page Synchronisation.
- Ne conserve que les ressources françaises dans l’installateur Windows.

# 0.1.5.3

- Utilise les noms réels des dossiers voiture iRacing dans l’archive et lors de la copie vers iRacing.
- Conserve le format de saison avec séparateur, par exemple `2022_S13`.
- Ajoute les circuits Glen, Mexique, Saint-Pétersbourg et Adelaide.
- Permet d’activer ou désactiver la surveillance automatique, tout en conservant l’import manuel.
- Ajoute les filtres fournisseur, catégorie, saison, voiture et circuit à la copie vers iRacing.
- Applique une Week commune à tous les setups d’une copie groupée.
- Rétablit le circuit dans l’arborescence configurable de copie vers iRacing.

# 0.1.4.3

- Importe automatiquement dans SQLite la liste des circuits et configurations trouvés dans `Documents\iRacing\lapfiles`.
- Utilise ce catalogue pour mieux reconnaître les circuits et actualise les métadonnées des setups déjà enregistrés.
- Ajoute les variantes Porsche Cup (`PCUP`, `992Cup`, `992.2Cup`) et confirme `NSX` comme Acura NSX GT3 Evo 22.

# 0.1.4.2

- Sélection d'une week inconnue avec treize boutons et confirmation obligatoire.
- Démarrage automatique de la surveillance locale avec l'application.
- Redémarrage de la surveillance après l'enregistrement des paramètres.
- Import garanti de tous les fichiers `.sto`, quel que soit leur nom ou la casse de l'extension.
- Prise en charge des numéros de saison positifs sans limite à quatre, comme `25S12` et `27S5`.
- Création des dossiers de copie `2025_S12`, `2027_S5` et équivalents.

# 0.1.4.1

- Arborescence de copie vers iRacing personnalisable depuis les paramètres.
- Ordre modifiable pour Saison, Fournisseur et Week avec aperçu dynamique.
- Conservation obligatoire des dossiers Voiture et Garage 61.
- Enregistrement de l'ordre choisi dans la base locale et restauration de l'ordre par défaut.
- Correction du nombre de fichiers affiché dans la confirmation de copie.
- Masquage du choix de conflit lorsqu'aucun conflit n'existe.

# 0.1.3.1

- Actualisation automatique des onglets Bibliothèque et À vérifier après chaque nouvel import.
- Rechargement systématique des données à l'ouverture de ces deux onglets.
- Reconnaissance de l'alias `NSX` comme Acura NSX GT3 Evo 22, catégorie GT3.
- Actualisation des anciennes entrées non identifiées lors de l'ouverture de la bibliothèque.

# 0.1.3.0

- Copie vers l'arborescence officielle des voitures iRacing, puis `Garage 61/Saison/Fournisseur/Week`.
- Détection automatique des semaines `W01` à `W13` dans les noms de fichiers.
- Demande obligatoire d'une semaine comprise entre 1 et 13 lorsqu'elle est inconnue.
- Blocage de la copie tant qu'une semaine manque et aperçu du chemin final avant confirmation.
- Catalogue étendu aux voitures GT3, GT4, GTE, LMP2, GTP et Porsche Cup sélectionnées.
- Redémarrage automatique de l'application après l'installation d'une mise à jour.

# 0.1.2.5

- Prise en charge sécurisée des archives RAR en plus des fichiers STO et ZIP.
- Réorganisation des archives existantes avec actualisation des chemins SQLite.
- Arborescence simplifiée en `Saison/Circuit/Voiture/Fournisseur`.
- Suppression du type de setup, comme `Race V2`, dans les chemins de dossiers.

# 0.1.2.1

- Suppression des cases de sélection inutiles dans la bibliothèque.
- Filtres alignés avec les colonnes Fichier, Fournisseur, Catégorie, Voiture, Circuit et Statut.
- Réimportation d’un fichier manquant dans l’archive et retour automatique au statut « À vérifier ».
- Libellés de statut rendus plus lisibles.

# 0.1.2.0

- Détection automatique des fichiers supprimés de l'archive.
- Statut « Fichier manquant » et exclusion de ces entrées des statistiques du tableau de bord.
- Retrait manuel et confirmé des entrées manquantes depuis la bibliothèque, sans suppression de fichier.
- Alignement précis des en-têtes et des données du tableau avec les cases de sélection.

# 0.1.1.6

- Alignement du tableau de la bibliothèque sur toute la largeur disponible.
- Actualisation automatique des métadonnées des setups déjà enregistrés.
- Conservation stricte des fichiers originaux et de leurs chemins d’archive.

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
