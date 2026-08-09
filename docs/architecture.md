# Architecture initiale

Le projet suit une architecture modulaire afin qu'une indisponibilité d'un
fournisseur n'empêche pas les autres fonctions de travailler.

- `App` présente l'interface et orchestre les cas d'utilisation.
- `Core` contient les modèles et règles sans dépendance vers Windows ou SQLite.
- `Infrastructure` gère la persistance, les fichiers, les empreintes et les journaux.
- `Providers` définit les connecteurs autorisés vers HYMO, GO et Grid & Go.
- `Integrations` définit la copie vers iRacing et l'export autorisé vers Garage61.

Les dépendances pointent vers `Core`. Aucun module fournisseur ne doit dépendre
d'un autre fournisseur. Les secrets ne transitent jamais dans les modèles métier
et ne doivent jamais apparaître dans les journaux.

