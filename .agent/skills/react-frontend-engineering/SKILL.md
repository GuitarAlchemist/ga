---
name: "React & Frontend Engineering"
description: "Standards and best practices for React, TypeScript, and 3D visualization development within the Guitar Alchemist frontend."
---

# React & Frontend Engineering Standards

This skill governs development within `ReactComponents/` and the `ga-client` application. It standardizes the usage of React, TypeScript, Material UI, and React Three Fiber.

## 1. Technology Stack Compliance

- **Framework**: React 18+ (Functional Components only).
- **Language**: TypeScript 5.0+ (Strict mode).
- **Build System**: Vite.
- **UI Library**: Material UI (MUI) v5.
- **3D Visualization**: React Three Fiber (R3F) / Three.js.
- **State Management**: React Context or Zustand (Avoid Redux unless necessary).
- **Testing**: Playwright (E2E/UI).

## 2. Component Architecture

### 2.1 File Structure
- **Paths**: `src/components/<Feature>/<ComponentName>.tsx`
- **Exports**: Use named exports. `export const MyComponent = ...`
- **Props Interface**: Define props interface immediately above the component.

```tsx
// DO:
interface FretboardProps {
    tuning: number[];
    showNotes: boolean;
}

export const Fretboard: React.FC<FretboardProps> = ({ tuning, showNotes }) => { ... };
```

### 2.2 Hooks & Logic
- **Custom Hooks**: Extract complex logic into `src/hooks/use<Feature>.ts`.
- **Side Effects**: Isolate `useEffect` calls. Avoid deep dependency chains.
- **Memoization**: Use `useMemo` for expensive calculations (e.g., generating fretboard geometry or theory analysis).

## 3. 3D & Visualization (React Three Fiber)

### 3.1 Separation of Concerns
- **Scene Graph**: Declarative R3F components for structural objects.
- **Animation**: Use `useFrame` for per-frame updates. **Never** put state updates or heavy logic inside `useFrame`.
- **References**: Use `useRef` to manipulate Three.js objects directly for performance (avoiding React re-renders for simple transforms).

```tsx
// DO:
const ref = useRef<THREE.Mesh>(null);
useFrame(() => {
    if (ref.current) ref.current.rotation.x += 0.01;
});
```

### 3.2 Performance
- **Instancing**: Use `<InstancedMesh>` for repetitive elements like frets or markers.
- **Geometry/Materials**: Reuse geometries and materials globally or memoize them.

## 4. Coding Style & Linting

### 4.1 TypeScript
- **No `any`**: Strictly define types. Use `unknown` if strict typing is impossible, then narrow.
- **Discriminated Unions**: Use them for state management (e.g., `type State = { status: 'loading' } | { status: 'success', data: Data }`).

### 4.2 Styling (MUI)
- **Theming**: Use the `sx` prop for one-off styles or `styled()` for reusable components.
- **Consistency**: Use theme tokens (colors, spacing) instead of hardcoded hex values or pixels.

```tsx
// DO:
<Box sx={{ p: 2, color: 'primary.main' }}>...

// DON'T:
<div style={{ padding: '16px', color: '#007FFF' }}>...
```

## 5. Testing (Playwright)

- **E2E Focus**: Tests should verify user flows (e.g., "User selects chord -> Diagram renders").
- **Visual Regression**: Use snapshots for sensitive rendering components (Fretboard, Charts).
- **Locators**: Prefer user-facing locators: `getByRole`, `getByText`, or `test-id` attributes.

## 6. How to Use This Skill
1. **New Components**: Scaffold using the Functional Component pattern.
2. **3D Features**: separating declarative Scene / Logic / Interaction layers.
3. **Refactoring**: Convert any Class Components or JavaScript files to FC/TypeScript.
