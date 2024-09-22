import React, { Suspense, useRef } from 'react';
import { Canvas, useFrame } from '@react-three/fiber';
import { OrbitControls, useGLTF } from '@react-three/drei';
import { Object3D } from 'three';

function HandModel() {
    const { scene } = useGLTF('/assets/Dorchester3D_com_rigged_hand.glb');
    const handRef = useRef<Object3D>(null);

    useFrame((_, delta) => {
        if (handRef.current) {
            handRef.current.rotation.y += delta * 0.5;
        }
    });

    return <primitive ref={handRef} object={scene} scale={0.01} />;
}

export default function RiggedHand() {
    return (
        <div style={{ height: '350px', width: '350px' }}>
            <Canvas camera={{ position: [0, 0, 1], fov: 50 }}>
                <ambientLight intensity={0.5} />
                <spotLight position={[10, 10, 10]} angle={0.15} penumbra={1} />
                <Suspense fallback={<div>Loading...</div>}>
                    <HandModel />
                </Suspense>
                <OrbitControls />
            </Canvas>
        </div>
    );
}

// Preload the model
useGLTF.preload('/assets/Dorchester3D_com_rigged_hand.glb');
