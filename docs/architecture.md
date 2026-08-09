# Architecture initiale

Le projet suit une architecture modulaire afin qu'une indisponibilité d'un
fournisseur n'empêche pas les autres fonctions de travailler.

- `App` présente l'interface et orchestre les cas d'utilisation.
- `Core` contient les modèles et règles sans dépendance vers Windows ou SQLite.
- `Infrastructure/Database` isole SQLite et les dépôts de données.
- `Infrastructure/Files` gère l'archive, les ZIP et les empreintes.
- `Infrastructure/Logging` contient la journalisation sans données sensibles.
- `Infrastructure/Security` protège les secrets avec les services Windows.
- `Providers/Hymo`, `Providers/GoSetups` et `Providers/GridAndGo` sont trois
  connecteurs indépendants qui partagent uniquement des contrats communs.
- `Providers/Synchronization` exécute seulement les fournisseurs choisis. Plusieurs
  fournisseurs peuvent travailler simultanément et chaque erreur reste isolée.
- `Integrations/Iracing` copie les setups validés sans toucher aux archives.
- `Integrations/Garage61` n'utilise qu'une méthode officiellement autorisée.
- `Integrations/Updates` vérifie les versions et l'intégrité des téléchargements.

Les dépendances pointent vers `Core`. Aucun module fournisseur ne doit dépendre
d'un autre fournisseur. L'orchestrateur collecte un résultat par fournisseur :
une panne HYMO ne bloque donc ni GO Setups ni Grid & Go. Les secrets ne transitent jamais dans les modèles métier
et ne doivent jamais apparaître dans les journaux.

## Base locale

La base SQLite contient une table `Setups` avec les métadonnées du fichier, son
empreinte SHA-256 unique, son classement, son statut et le résultat Garage61. Les
index couvrent les recherches par fournisseur, catégorie, statut, voiture, circuit
et saison. La table `ApplicationSettings` conserve le dossier d'archive : le
sélecteur est affiché uniquement si aucun chemin n'a encore été enregistré.

## Surveillance et confidentialité

Seuls le dossier Téléchargements et les dossiers explicitement configurés des
applications officielles peuvent être surveillés. `Documents\\iRacing\\setups` et
tous ses sous-dossiers sont refusés par le code afin de ne pas mélanger les setups
personnels avec ceux des fournisseurs. Un setup privé, non validé ou non approuvé
manuellement ne peut pas être proposé à l'export Garage61.
