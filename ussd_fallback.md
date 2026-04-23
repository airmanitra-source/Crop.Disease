# USSD/SMS Fallback — Product & Business Adaptation
## Crop Disease Classifier · AIMS KTT Hackathon T2.1

---

## 1. Contexte terrain

Le déploiement cible des agriculteurs en zones rurales avec :
- **Feature phone uniquement** (pas de smartphone)
- **Réseau 2G/EDGE intermittent**
- **Analphabétisme partiel** → messages courts, visuels, bilingues
- **Langues** : Kinyarwanda (principal) + Français (officiel)

---

## 2. Workflow 3 étapes (sans smartphone)

```
┌─────────────────────────────────────────────────────────────────────┐
│  ÉTAPE 1 — CAPTURE PHOTO                                            │
│  L'agriculteur prend une photo de la feuille malade avec son        │
│  téléphone (même basique avec appareil photo).                      │
│  Relais possible :                                                  │
│    • Agent terrain (extension officer) avec smartphone              │
│    • Kiosque de la coopérative                                      │
│    • Agent de village équipé d'une tablette hors-ligne              │
└────────────────────────┬────────────────────────────────────────────┘
                         │
                         ▼
┌─────────────────────────────────────────────────────────────────────┐
│  ÉTAPE 2 — UPLOAD (via agent/kiosque)                               │
│  POST https://api.cropdisease.rw/predict                            │
│  multipart/form-data  →  image JPEG < 500 KB                        │
│  Fonctionne en 2G (payload réduite, JPEG compressé)                 │
└────────────────────────┬────────────────────────────────────────────┘
                         │
                         ▼
┌─────────────────────────────────────────────────────────────────────┐
│  ÉTAPE 3 — LIVRAISON DU DIAGNOSTIC                                  │
│  → USSD push ou SMS envoyé au numéro de l'agriculteur               │
│  → Template bilingue (Kinyarwanda + Français) < 160 caractères     │
└─────────────────────────────────────────────────────────────────────┘
```

---

## 3. Templates USSD/SMS (< 160 caractères)

### 3.1 Rouille commune (`Corn_Common_Rust`)
```
[RW]Umutuku ku mibago (90%). Koresha Mancozebe 2kg/ha.
[FR]Rouille commune (90%). Appliquer Mancozèbe 2kg/ha.
```

### 3.2 Brûlure des feuilles (`Northern_Leaf_Blight`)
```
[RW]Indwara ikonjesha ibabi (85%). Koresha Propiconazole 0.5L/ha.
[FR]Helminthosporiose (85%). Appliquer Propiconazole 0.5L/ha.
```

### 3.3 Cercosporiose grise (`Gray_Leaf_Spot`)
```
[RW]Amabara y'iguruka (78%). Koresha Trifloxystrobine.
[FR]Cercosporiose (78%). Trifloxystrobine+Tébuconazole.
```

### 3.4 Plante saine (`Corn_Healthy`)
```
[RW]Igihingwa ni muzima. Komeza imirimo isanzwe.
[FR]Plante saine. Continuer les bonnes pratiques.
```

### 3.5 Escalade low-confidence (< 60 % de confiance)
```
[RW]Ifoto ntabwo iri sobanutse neza. Fata ifoto nziza y'icyatsi.
[FR]Photo peu claire. Prenez une 2ème photo sous un autre angle.
```

---

## 4. Économie unitaire (Unit Economics)

| Métrique | Valeur |
|---|---|
| Coût par diagnostic (cloud inference) | ~$0.003 USD |
| Coût pour 1 000 agriculteurs | **~$3 USD** |
| Valeur estimée récolte maïs sauvée | $250 USD/ha |
| Valeur sauvée pour 1 000 agriculteurs (1 ha chacun) | **$250 000 USD** |
| **ROI estimé** | **~83 000x** |

> Source prix maïs : FAO Rwanda 2024. Hypothèse : 1 traitement précoce évite 40 % de pertes.

---

## 5. Acteurs du réseau de distribution

| Acteur | Rôle | Équipement |
|---|---|---|
| Agriculteur | Capture photo, reçoit SMS | Feature phone |
| Agent de terrain | Relaie upload, explique résultat | Smartphone + app |
| Kiosque coopérative | Point d'upload physique | Tablette + WiFi |
| Agent de village | Dernier kilomètre | Smartphone offline-first |

---

## 6. Flux USSD (interface interactive alternative)

```
*123*DISEASE#
→ 1. Envoyer photo via WhatsApp/Telegram (agent)
→ 2. Demander diagnostic par SMS (numéro de référence)
→ 3. Voir dernier résultat
```

---

## 7. Considérations de sécurité / confidentialité

- **Aucune donnée personnelle stockée** : l'image est traitée en mémoire et supprimée.
- **HTTPS obligatoire** sur le endpoint `/predict`.
- **Numéro de téléphone** utilisé uniquement pour livrer le SMS, non stocké.

---

## 8. Stretch Goal : Export Grad-CAM (pour agent de terrain)

L'API peut retourner une heatmap Grad-CAM dans la réponse JSON (`rationale_image_base64`) 
permettant à l'agent de terrain d'**expliquer visuellement** la décision au comité de la coopérative.

Activation : `GET /predict?explain=true`

---

*Document préparé dans le cadre de l'AIMS KTT Hackathon T2.1 — Compressed Crop Disease Classifier.*
