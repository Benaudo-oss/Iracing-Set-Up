# MAJ01 — Uniformisation des noms de dossiers

## Objectif

Revoir l’écriture des dossiers créés par l’application afin d’obtenir une arborescence cohérente, lisible et sans doublons dus aux variantes de noms.

Après un déplacement interne réussi, les anciens dossiers devenus totalement vides sont supprimés en remontant l’arborescence, sans jamais supprimer la racine de l’archive.

## Fournisseurs

- Utiliser une seule écriture officielle par fournisseur dans les dossiers.
- Reconnaître les alias et variantes lors de l’import, mais toujours les convertir vers le nom officiel pour l’archive.
- Empêcher qu’un même fournisseur possède plusieurs dossiers à cause d’une différence d’abréviation, de casse ou d’espacement.

Noms officiels à conserver :

- `HYMO`
- `GO Setups`
- `Grid & Go`
- `VRS`
- `SRS`
- `P1Doks`
- `Coach Dave Academy (CDA)`

## Weeks

Utiliser exclusivement les formats suivants dans l’archive :

- `Week 01` à `Week 13`
- `Week NEC`
- `Sans Week`
- `Week inconnue`

Les variantes rencontrées pendant l’analyse (`Week 5`, `Week_5`, `Week-05`, `W05`, etc.) doivent être reconnues, puis normalisées vers le format officiel.

`Sans Week` reste un état volontaire, définitif et distinct de `Week inconnue`.

## Circuits

- Écrire dans l’archive le nom complet officiel du circuit.
- Continuer à reconnaître les abréviations, alias et fautes courantes pendant l’analyse.
- Ne jamais utiliser ces alias comme noms de dossiers.
- Toutes les variantes d’un même circuit doivent aboutir à un seul dossier officiel.

## Exemples d’arborescences attendues

```text
2026_S3\Week 05\Circuit des 24 Heures du Mans\bmwm4gt3\VRS\setup.sto
```

```text
2026_S3\Week NEC\Nürburgring Combined\porsche963gtp\HYMO\setup.sto
```

```text
2026_S3\Week inconnue\Autodromo Internazionale Enzo e Dino Ferrari\porsche991rsr\Grid & Go\setup.sto
```

## État

MAJ01 est implémentée localement. La réorganisation reste déclenchée manuellement
depuis les paramètres et affiche un aperçu avant confirmation. Aucun déplacement
d’archive utilisateur n’est exécuté pendant la compilation ou les tests.
