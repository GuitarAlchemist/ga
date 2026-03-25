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

const PLANETS: PlanetDef[] = [
  {
    name: 'mercury', radius: 0.15, distance: 4, speed: 4.7,
    fragment: `
      varying vec3 vPos;
      vec3 h(vec3 p){p=fract(p*vec3(443.8,441.4,437.2));p+=dot(p,p.yzx+19.19);return fract((p.xxy+p.yxx)*p.zyx);}
      float n(vec3 p){vec3 i=floor(p),f=fract(p);f=f*f*(3.-2.*f);return mix(mix(dot(h(i),f),dot(h(i+vec3(1,0,0)),f-vec3(1,0,0)),f.x),mix(dot(h(i+vec3(0,1,0)),f-vec3(0,1,0)),dot(h(i+vec3(1,1,0)),f-vec3(1,1,0)),f.x),f.y)*.5+.5;}
      void main(){float c=n(vPos*15.)*.5+n(vPos*30.)*.3;gl_FragColor=vec4(vec3(.45,.42,.4)*(.5+c),1.);}`,
  },
  {
    name: 'venus', radius: 0.35, distance: 6.5, speed: 3.5,
    fragment: `
      uniform float uTime;varying vec3 vPos;
      void main(){float swirl=sin(vPos.y*8.+vPos.x*3.+uTime*.5)*.5+.5;vec3 col=mix(vec3(.9,.7,.2),vec3(.95,.85,.4),swirl);float haze=.7+.3*sin(uTime*.3);gl_FragColor=vec4(col*haze,1.);}`,
  },
  {
    name: 'earth', radius: 0.4, distance: 9, speed: 2.98,
    fragment: `
      uniform float uTime;varying vec3 vPos;varying vec3 vNormal;
      vec3 h(vec3 p){p=fract(p*vec3(443.8,441.4,437.2));p+=dot(p,p.yzx+19.19);return fract((p.xxy+p.yxx)*p.zyx);}
      float n(vec3 p){vec3 i=floor(p),f=fract(p);f=f*f*(3.-2.*f);return mix(mix(dot(h(i),f),dot(h(i+vec3(1,0,0)),f-vec3(1,0,0)),f.x),mix(dot(h(i+vec3(0,1,0)),f-vec3(0,1,0)),dot(h(i+vec3(1,1,0)),f-vec3(1,1,0)),f.x),f.y)*.5+.5;}
      float fbm(vec3 p){float v=0.,a=.5;for(int i=0;i<3;i++){v+=a*n(p);p*=2.1;a*=.5;}return v;}
      void main(){
        vec3 sun=normalize(vec3(1.,.3,.5));float day=smoothstep(-.1,.3,dot(vNormal,sun));
        float land=smoothstep(.48,.52,fbm(vPos*2.5));float lat=abs(vPos.y/length(vPos));
        vec3 ocean=vec3(.01,.04,.15);vec3 green=mix(vec3(.04,.12,.03),vec3(.7,.72,.75),smoothstep(.7,.85,lat));
        vec3 surf=mix(ocean,green,land);vec3 lit=surf*(.06+.94*day);
        float city=land*(1.-day)*smoothstep(.55,.7,n(vPos*20.))*.7;lit+=vec3(1.,.85,.4)*city;
        float cl=smoothstep(.5,.65,fbm(vPos*3.+uTime*.01))*.35*day;lit+=vec3(.8)*cl;
        gl_FragColor=vec4(lit,1.);}`,
  },
  {
    name: 'mars', radius: 0.25, distance: 12, speed: 2.4,
    fragment: `
      varying vec3 vPos;
      vec3 h(vec3 p){p=fract(p*vec3(443.8,441.4,437.2));p+=dot(p,p.yzx+19.19);return fract((p.xxy+p.yxx)*p.zyx);}
      float n(vec3 p){vec3 i=floor(p),f=fract(p);f=f*f*(3.-2.*f);return mix(mix(dot(h(i),f),dot(h(i+vec3(1,0,0)),f-vec3(1,0,0)),f.x),mix(dot(h(i+vec3(0,1,0)),f-vec3(0,1,0)),dot(h(i+vec3(1,1,0)),f-vec3(1,1,0)),f.x),f.y)*.5+.5;}
      void main(){float caps=smoothstep(.85,.95,abs(vPos.y/length(vPos)));float t=n(vPos*8.);vec3 col=mix(vec3(.7,.25,.05),vec3(.85,.4,.15),t);col=mix(col,vec3(.9),caps);gl_FragColor=vec4(col,1.);}`,
  },
  {
    name: 'jupiter', radius: 1.2, distance: 17, speed: 1.3,
    fragment: `
      uniform float uTime;varying vec3 vPos;
      void main(){float y=vPos.y/length(vPos);float bands=sin(y*25.+sin(y*7.)*.5)*.5+.5;float spot=1.-smoothstep(.0,.15,length(vec2(vPos.x/length(vPos)-.3,y+.2)));vec3 col=mix(vec3(.85,.6,.3),vec3(.95,.85,.6),bands);col=mix(col,vec3(.8,.2,.1),spot*.6);gl_FragColor=vec4(col,1.);}`,
  },
  {
    name: 'saturn', radius: 1.0, distance: 22, speed: 0.97,
    fragment: `
      uniform float uTime;varying vec3 vPos;
      void main(){float y=vPos.y/length(vPos);float bands=sin(y*15.)*.5+.5;vec3 col=mix(vec3(.9,.8,.5),vec3(.75,.65,.4),bands);gl_FragColor=vec4(col,1.);}`,
  },
  {
    name: 'uranus', radius: 0.6, distance: 27, speed: 0.68, tilt: 1.71,
    fragment: `
      varying vec3 vPos;
      void main(){float y=vPos.y/length(vPos);float b=sin(y*12.)*.1;vec3 col=vec3(.55+b,.8+b,.85+b);gl_FragColor=vec4(col*.7,1.);}`,
  },
  {
    name: 'neptune', radius: 0.55, distance: 32, speed: 0.54,
    fragment: `
      uniform float uTime;varying vec3 vPos;
      void main(){float y=vPos.y/length(vPos);float b=sin(y*10.+uTime*.2)*.15;vec3 col=vec3(.1,.15+b,.6+b);gl_FragColor=vec4(col,1.);}`,
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
