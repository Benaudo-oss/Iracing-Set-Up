# Architecture initiale

Le projet suit une architecture modulaire afin qu'une indisponibilité d'un
fournisseur n'empêche pas les autres fonctions de travailler.

- `App` présente l'interface et orchestre les cas d'utilisation.
- `Core` contient les modèles et règles sans dépendance vers Windows ou SQLite.
- `Infrastructure/Database` isole SQLite et les dépôts de données.
- `Infrastructure/Files` gère l'archive, les ZIP et les empreintes.
- `Infrastructure/Logging` contient la journalisation sans données sensibles.
- `Infrastructure/Iracing` copie les setups validés sans toucher aux archives.
- Les fournisseurs sont reconnus lors de l'import local ; aucun connecteur de
  téléchargement direct n'est embarqué.
- `Integrations/Updates` vérifie les versions et l'intégrité des téléchargements.

Les dépendances pointent vers `Core`. Les imports provenant de chaque dossier
surveillé sont traités indépendamment. Aucun identifiant fournisseur n'est demandé
ou conservé par l'application.

## Base locale

La base SQLite contient une table `Setups` avec les métadonnées du fichier, son
empreinte SHA-256 unique, son classement, son statut et ses informations de copie. La
table `SchemaMigrations` enregistre les évolutions appliquées ; une migration n'est
donc jamais rejouée à chaque lancement. Les
index couvrent les recherches par fournisseur, catégorie, statut, voiture, circuit
et saison. La table `ApplicationSettings` conserve le dossier d'archive : le
sélecteur est affiché uniquement si aucun chemin n'a encore été enregistré.

## Surveillance et confidentialité

Seuls le dossier Téléchargements et les dossiers explicitement configurés des
applications officielles peuvent être surveillés. `Documents\\iRacing\\setups` et
tous ses sous-dossiers sont refusés par le code afin de ne pas mélanger les setups
personnels avec ceux des fournisseurs. Aucun upload direct vers Garage61 n'est
effectué par l'application.

## Bibliothèque locale

Le service d'import accepte les fichiers `.sto`, les archives ZIP et les dossiers
existants. Il analyse les noms et chemins sans jamais renommer les fichiers, calcule
le SHA-256 puis classe les originaux sous
`Saison/Circuit/Voiture/Fournisseur`. Le fichier source est uniquement lu.
Les imports acceptent les fichiers `.sto` ainsi que les archives `.zip` et `.rar`,
avec contrôle des chemins, du nombre d’entrées et des tailles avant extraction.
Une collision de nom avec un contenu différent est conservée dans un sous-dossier
`Conflits/<début-du-SHA>` ; aucun fichier existant n'est remplacé.

La surveillance combine `FileSystemWatcher` avec un balayage au démarrage et à la
demande. Les événements passent dans une file séquentielle ; un fichier doit rester
stable et lisible plusieurs fois avant import. Les fichiers temporaires sont ignorés
et seuls `.sto`, `.zip` et `.rar` sont acceptés.

## Catalogues

`Core/Catalog/SetupCatalog` est la source unique des fournisseurs, catégories,
voitures actuelles, dossiers internes iRacing et codes fournisseurs Team. Les alias
historiques de voitures et de circuits restent regroupés dans l'analyseur de
métadonnées. Le catalogue extrait de `Documents\iRacing\lapfiles` réutilise cette
même résolution des noms de circuits.

## Validation et historique

Tout nouvel import reçoit le statut `À vérifier`. Les validations et refus peuvent
être individuels ou groupés ; le service refuse techniquement une action portant
sur plusieurs setups si la confirmation explicite n'est pas fournie. La note sur 5,
le commentaire, les anciens et nouveaux statuts sont conservés dans la table
`SetupChangeHistory` avec la date de chaque changement.

## Copie vers iRacing

Le dossier `Documents\iRacing\setups` est détecté quand il existe et reste
sélectionnable manuellement. Seuls les setups ayant le statut `Valide` entrent
dans le plan de copie, avec une seconde vérification juste avant l’écriture.
L’aperçu indique chaque destination et chaque conflit. Les choix possibles sont
ignorer ou conserver les deux ; aucun écrasement implicite n’est autorisé. La
copie lit l’original depuis l’archive sans jamais le déplacer ou le supprimer.

## Sécurité et sauvegarde

Le filtre de journalisation masque mots de passe, jetons, clés API,
en-têtes d’autorisation et cookies ; les messages d’exception externes ne sont pas
écrits. Les chemins de périphérique, sorties de dossier et flux NTFS alternatifs
sont refusés. Les ZIP sont contrôlés avant extraction (traversée, liens, doublons,
nombre, tailles et taux de compression). Les chemins sources devenus inutiles sont
effacés au démarrage. La sauvegarde utilise l’API SQLite afin de produire une base
cohérente pendant que l’application est ouverte.

Les erreurs des opérations WinUI sont interceptées à la frontière de l'interface,
présentées sans détails sensibles et écrites dans des journaux quotidiens expurgés.
Les échecs de la surveillance utilisent le même journal. Les panneaux qui ne
représentaient aucun état réel ont été retirés de l'interface.

## Tests

Les tests couvrent l'import, les archives, SHA-256, SQLite, les migrations, la
validation, la surveillance, la copie iRacing et la logique partagée de recherche
et de filtres. Les tests de présentation ciblent du code indépendant de WinUI afin
de rester exécutables dans l'intégration continue. Une vérification visuelle de
l'application Windows complète les tests automatisés avant publication.

## Mises à jour

Le canal stable interroge l’API GitHub Releases et cherche deux assets portant le
nom versionné attendu : l’installateur et son fichier `.sha256`. Le téléchargement
reste temporaire jusqu’à la comparaison SHA-256. L’installation et le retour
arrière lancent ensuite Inno Setup dans un processus séparé avant la fermeture de
l’application. Chaque installateur conserve une copie de lui-même dans les données
locales, ce qui rend la version précédente disponible sans toucher à la base SQLite.
