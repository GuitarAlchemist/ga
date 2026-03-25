// src/components/PrimeRadiant/SolarSystem.ts
// Procedural mini solar system — Sun + 8 planets + Moon + Saturn rings
// Co-designed with GPT-4o, enhanced shaders by Claude Opus

import * as THREE from 'three';

const VERT = /* glsl */ `
  varying vec3 vPos;
  varying vec3 vNormal;
  varying vec2 vUv;
  void main() {
    vPos = position;
    vNormal = normalize(normalMatrix * normal);
    vUv = uv;
    gl_Position = projectionMatrix * modelViewMatrix * vec4(position, 1.0);
  }
`;

interface PlanetDef {
  name: string;
  radius: number;
  distance: number;
  speed: number;
  tilt?: number;
  fragment: string;
}

// Realistic proportions: log-scaled distances, sqrt-scaled radii, Kepler periods
// Real AU: Mer 0.39, Ven 0.72, Ear 1.0, Mar 1.52, Jup 5.2, Sat 9.5, Ura 19.2, Nep 30.1
// Log-scaled to fit in ~35 units; speed ∝ distance^-1.5 (Kepler's 3rd law)
const NOISE_LIB = `
float h(vec3 p){return fract(sin(dot(p,vec3(1.3,1.7,1.9)))*43758.5);}
float n(vec3 x){vec3 i=floor(x),f=fract(x);f=f*f*(3.-2.*f);return mix(mix(mix(h(i),h(i+vec3(1,0,0)),f.x),mix(h(i+vec3(0,1,0)),h(i+vec3(1,1,0)),f.x),f.y),mix(mix(h(i+vec3(0,0,1)),h(i+vec3(1,0,1)),f.x),mix(h(i+vec3(0,1,1)),h(i+vec3(1,1,1)),f.x),f.y),f.z);}
float fbm(vec3 p){float v=0.0,a=0.5;for(int i=0;i<5;i++,p*=2.){v+=a*n(p);a*=0.5;}return v;}
`;

const PLANETS: PlanetDef[] = [
  {
    name: 'mercury', radius: 0.12, distance: 4.5, speed: 8.3,
    fragment: NOISE_LIB + `
      varying vec3 vPos;
      void main(){float c=n(vPos*15.)*.5+n(vPos*30.)*.3+n(vPos*60.)*.1;gl_FragColor=vec4(vec3(.5,.48,.45)*(.4+c*.6),1.);}`,
  },
  {
    name: 'venus', radius: 0.3, distance: 6.5, speed: 4.5,
    fragment: NOISE_LIB + `
      uniform float uTime;varying vec3 vPos;
      void main(){float sw=fbm(vPos*3.+vec3(uTime*.02,0,uTime*.01));vec3 col=mix(vec3(.85,.65,.2),vec3(.95,.85,.5),sw);float haze=.75+.25*sin(vPos.y*4.+uTime*.3);gl_FragColor=vec4(col*haze,1.);}`,
  },
  {
    name: 'earth', radius: 0.35, distance: 9, speed: 3.0,
    fragment: NOISE_LIB + `
      uniform float uTime;varying vec3 vPos;varying vec3 vNormal;
      void main(){
        vec3 sun=normalize(vec3(1.,.3,.5));float day=smoothstep(-.1,.3,dot(vNormal,sun));
        float land=smoothstep(.48,.52,fbm(vPos*2.5));float lat=abs(vPos.y/length(vPos));
        vec3 ocean=vec3(.01,.04,.15+.2*smoothstep(.3,.7,-vPos.y));
        vec3 biome=land<.25?vec3(.72,.52,.24):land<.5?vec3(.1,.3,.1):lat<.5?vec3(.9,.9,.85):vec3(.7,.7,.75);
        vec3 surf=mix(ocean,biome,land),lit=surf*(.06+.94*day);
        float city=land*(1.-day)*smoothstep(.55,.7,n(vPos*20.))*.7;lit+=vec3(1.,.85,.4)*city;
        float cl=smoothstep(.5,.65,fbm(vPos*3.+uTime*.01))*.35*day;lit+=vec3(.8)*cl;
        float aurora=exp(-10.*(lat-.8*lat*(1.-day)))*(1.-day)*smoothstep(.9,.95,fbm(vPos*10.));
        lit+=vec3(.4,.9,1.)*aurora;
        gl_FragColor=vec4(lit,1.);}`,
  },
  {
    name: 'mars', radius: 0.18, distance: 11.5, speed: 2.0,
    fragment: NOISE_LIB + `
      varying vec3 vPos;
      void main(){float lat=abs(vPos.y/length(vPos));float caps=smoothstep(.82,.92,lat);float t=fbm(vPos*4.);vec3 col=mix(vec3(.65,.22,.05),vec3(.8,.35,.12),t);col=mix(col,vec3(.92,.9,.88),caps);gl_FragColor=vec4(col,1.);}`,
  },
  {
    name: 'jupiter', radius: 0.9, distance: 17, speed: 0.85,
    fragment: NOISE_LIB + `
      uniform float uTime;varying vec3 vPos;
      void main(){
        float y=vPos.y/length(vPos),layers=(sin(y*25.)+sin(y*50.+uTime)*.1)*.5+.5;
        vec3 base=vec3(.85,.6,.3)*layers,band=vec3(fbm(vPos*vec3(20,4,1)+.5*uTime),.5,.3);
        vec3 col=mix(base,band,0.5);float zb=n(vPos*vec3(4,20,1)+uTime*.1);col=mix(col,vec3(.95,.85,.6),zb);
        float spot=1.-smoothstep(.0,.18,length(vec2(vPos.x/length(vPos)-.3,y+.2)));
        vec3 sc=vec3(.8,.2,.1)*fbm(vPos*vec3(4,1,4)+uTime);col=mix(col,sc,spot);
        gl_FragColor=vec4(col,1.);}`,
  },
  {
    name: 'saturn', radius: 0.75, distance: 22, speed: 0.55,
    fragment: NOISE_LIB + `
      uniform float uTime;varying vec3 vPos;
      void main(){float y=vPos.y/length(vPos);float b=sin(y*18.+sin(y*5.)*.3)*.5+.5;float t=n(vPos*vec3(10,3,10));vec3 col=mix(vec3(.85,.75,.45),vec3(.7,.6,.35),b+t*.2);gl_FragColor=vec4(col,1.);}`,
  },
  {
    name: 'uranus', radius: 0.45, distance: 27, speed: 0.3, tilt: 1.71,
    fragment: `
      varying vec3 vPos;
      void main(){float y=vPos.y/length(vPos);float b=sin(y*12.)*.08;vec3 col=vec3(.52+b,.78+b,.83+b);gl_FragColor=vec4(col*.75,1.);}`,
  },
  {
    name: 'neptune', radius: 0.42, distance: 32, speed: 0.2,
    fragment: `
      uniform float uTime;varying vec3 vPos;
      void main(){float y=vPos.y/length(vPos);float b=sin(y*14.+uTime*.15)*.12;vec3 col=vec3(.08,.12+b,.55+b);gl_FragColor=vec4(col,1.);}`,
  },
];

export function createSolarSystem(scale: number): THREE.Group {
  const group = new THREE.Group();
  group.userData.isSolarSystem = true;

  // ── Sun ──
  const sunGeo = new THREE.SphereGeometry(2 * scale, 32, 32);
  const sunMat = new THREE.ShaderMaterial({
    uniforms: { uTime: { value: 0 } },
    vertexShader: VERT,
    fragmentShader: `
      uniform float uTime;varying vec3 vPos;varying vec3 vNormal;
      void main(){
        float pulse=.85+.15*sin(uTime*2.);
        float fresnel=pow(1.-abs(dot(vNormal,vec3(0,0,1))),.8);
        vec3 col=vec3(1.,.9,.6)*pulse+vec3(1.,.5,.1)*fresnel*.5;
        gl_FragColor=vec4(col,1.);
      }`,
    blending: THREE.AdditiveBlending,
    transparent: true,
  });
  const sun = new THREE.Mesh(sunGeo, sunMat);
  sun.name = 'sun';
  group.add(sun);

  // Sun corona glow
  const coronaGeo = new THREE.SphereGeometry(3 * scale, 16, 16);
  const coronaMat = new THREE.ShaderMaterial({
    uniforms: {},
    vertexShader: `varying vec3 vNormal;varying vec3 vViewDir;void main(){vNormal=normalize(normalMatrix*normal);vec4 wp=modelMatrix*vec4(position,1.);vViewDir=normalize(cameraPosition-wp.xyz);gl_Position=projectionMatrix*modelViewMatrix*vec4(position,1.);}`,
    fragmentShader: `varying vec3 vNormal;varying vec3 vViewDir;void main(){float f=1.-abs(dot(vNormal,vViewDir));f=pow(f,2.5);gl_FragColor=vec4(1.,.7,.2,f*.3);}`,
    transparent: true, side: THREE.BackSide, depthWrite: false, blending: THREE.AdditiveBlending,
  });
  group.add(new THREE.Mesh(coronaGeo, coronaMat));

  // ── Planets ──
  const planetMeshes: { mesh: THREE.Mesh; orbit: THREE.Group; def: PlanetDef }[] = [];

  for (const def of PLANETS) {
    const orbit = new THREE.Group();
    orbit.name = `orbit-${def.name}`;

    const geo = new THREE.SphereGeometry(def.radius * scale, 24, 24);
    const mat = new THREE.ShaderMaterial({
      uniforms: { uTime: { value: 0 } },
      vertexShader: VERT,
      fragmentShader: def.fragment,
    });
    const mesh = new THREE.Mesh(geo, mat);
    mesh.name = def.name;
    mesh.position.x = def.distance * scale;
    if (def.tilt) mesh.rotation.z = def.tilt;
    orbit.add(mesh);

    // Atmosphere rim for Earth and Venus
    if (def.name === 'earth' || def.name === 'venus') {
      const atmoGeo = new THREE.SphereGeometry((def.radius + 0.05) * scale, 16, 16);
      const atmoCol = def.name === 'earth' ? '0.3,0.5,1.0' : '1.0,0.8,0.3';
      const atmoMat = new THREE.ShaderMaterial({
        uniforms: {},
        vertexShader: `varying vec3 vN;varying vec3 vV;void main(){vN=normalize(normalMatrix*normal);vec4 w=modelMatrix*vec4(position,1.);vV=normalize(cameraPosition-w.xyz);gl_Position=projectionMatrix*modelViewMatrix*vec4(position,1.);}`,
        fragmentShader: `varying vec3 vN;varying vec3 vV;void main(){float f=pow(1.-abs(dot(vN,vV)),3.);gl_FragColor=vec4(${atmoCol},f*.4);}`,
        transparent: true, side: THREE.BackSide, depthWrite: false, blending: THREE.AdditiveBlending,
      });
      const atmo = new THREE.Mesh(atmoGeo, atmoMat);
      atmo.position.copy(mesh.position);
      orbit.add(atmo);
    }

    // Moon for Earth
    if (def.name === 'earth') {
      const moonGeo = new THREE.SphereGeometry(0.1 * scale, 12, 12);
      const moonMat = new THREE.ShaderMaterial({
        uniforms: {},
        vertexShader: VERT,
        fragmentShader: `varying vec3 vPos;void main(){float c=fract(sin(dot(vPos*40.,vec3(12.99,78.23,45.16)))*43758.55);gl_FragColor=vec4(vec3(.55,.53,.5)*(.4+c*.6),1.);}`,
      });
      const moon = new THREE.Mesh(moonGeo, moonMat);
      moon.name = 'moon';
      moon.position.set(def.distance * scale + 1.0 * scale, 0, 0);
      orbit.add(moon);
      orbit.userData.moonRef = moon;
      orbit.userData.earthRef = mesh;
    }

    // Saturn ring
    if (def.name === 'saturn') {
      const ringGeo = new THREE.RingGeometry(1.3 * scale, 2.0 * scale, 64);
      const ringMat = new THREE.ShaderMaterial({
        uniforms: {},
        vertexShader: `varying vec2 vUv;void main(){vUv=uv;gl_Position=projectionMatrix*modelViewMatrix*vec4(position,1.);}`,
        fragmentShader: `varying vec2 vUv;void main(){float r=length(vUv-.5)*2.;float bands=sin(r*40.)*.5+.5;float alpha=smoothstep(0.,.1,r)*smoothstep(1.,.8,r)*.5;gl_FragColor=vec4(.9,.8,.5,alpha*bands);}`,
        transparent: true, side: THREE.DoubleSide, depthWrite: false,
      });
      const ring = new THREE.Mesh(ringGeo, ringMat);
      ring.rotation.x = Math.PI / 2.2;
      ring.position.copy(mesh.position);
      orbit.add(ring);
    }

    group.add(orbit);
    planetMeshes.push({ mesh, orbit, def });
  }

  group.userData.planets = planetMeshes;
  return group;
}

export function updateSolarSystem(group: THREE.Group, time: number): void {
  const planets = group.userData.planets as { mesh: THREE.Mesh; orbit: THREE.Group; def: PlanetDef }[] | undefined;
  if (!planets) return;

  // Sun shader time
  const sun = group.getObjectByName('sun') as THREE.Mesh | undefined;
  if (sun) {
    const mat = sun.material as THREE.ShaderMaterial;
    if (mat.uniforms?.uTime) mat.uniforms.uTime.value = time;
  }

  for (const { mesh, orbit, def } of planets) {
    // Orbit rotation
    orbit.rotation.y = time * def.speed * 0.1;

    // Planet self-rotation
    mesh.rotation.y = time * 0.5;

    // Shader time
    const mat = mesh.material as THREE.ShaderMaterial;
    if (mat.uniforms?.uTime) mat.uniforms.uTime.value = time;

    // Moon orbit around Earth
    if (orbit.userData.moonRef) {
      const moon = orbit.userData.moonRef as THREE.Mesh;
      const earth = orbit.userData.earthRef as THREE.Mesh;
      const moonDist = 1.0;
      moon.position.set(
        earth.position.x + Math.cos(time * 2) * moonDist,
        0,
        earth.position.z + Math.sin(time * 2) * moonDist,
      );
    }
  }
}
