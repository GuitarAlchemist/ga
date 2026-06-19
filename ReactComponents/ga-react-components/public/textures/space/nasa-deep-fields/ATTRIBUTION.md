# NASA Deep Field Skyboxes

Prime Radiant can use these public NASA-hosted images as optional skybox textures:

- `skybox-hubble-ultra-deep-field.jpg` - Hubble Ultra Deep Field, NASA/ESA/STScI. Source: https://science.nasa.gov/asset/hubble/hubble-ultra-deep-field/
- `skybox-jwst-first-deep-field.png` - Webb's First Deep Field, NASA/ESA/CSA/STScI. Source: https://science.nasa.gov/asset/webb/webbs-first-deep-field-nircam-image/

These source images are deep-field observations, not equirectangular 360-degree panoramas. The renderer tiles them across the interior sky sphere to avoid stretching a single square image over the entire background.
