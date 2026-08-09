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

## Bibliothèque locale

Le service d'import accepte les fichiers `.sto`, les archives ZIP et les dossiers
existants. Il analyse les noms et chemins sans jamais renommer les fichiers, calcule
le SHA-256 puis classe les originaux sous
`Saison/Circuit/Voiture/Fournisseur/Type`. Le fichier source est uniquement lu.
Une collision de nom avec un contenu différent est conservée dans un sous-dossier
`Conflits/<début-du-SHA>` ; aucun fichier existant n'est remplacé.

La surveillance combine `FileSystemWatcher` avec un balayage au démarrage et à la
demande. Les événements passent dans une file séquentielle ; un fichier doit rester
stable et lisible plusieurs fois avant import. Les fichiers temporaires sont ignorés
et seuls `.sto` et `.zip` sont acceptés.

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

Les secrets sont confiés au Gestionnaire d’identifiants Windows et ne transitent
pas par SQLite. Le filtre de journalisation masque mots de passe, jetons, clés API,
en-têtes d’autorisation et cookies ; les messages d’exception externes ne sont pas
écrits. Les chemins de périphérique, sorties de dossier et flux NTFS alternatifs
sont refusés. Les ZIP sont contrôlés avant extraction (traversée, liens, doublons,
nombre, tailles et taux de compression). Les chemins sources devenus inutiles sont
effacés au démarrage. La sauvegarde utilise l’API SQLite afin de produire une base
cohérente pendant que l’application est ouverte.
