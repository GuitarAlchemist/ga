import SolarSystem from "./components/SolarSystem.tsx";

function App() {
    return (
        <div className="App" style={{ 
            backgroundColor: '#000', 
            minHeight: '100vh',
            display: 'flex',
            flexDirection: 'column',
            alignItems: 'center',
            padding: '20px'
        }}>
            <h1 style={{ color: '#fff', marginBottom: '20px' }}>WebGPU Solar System</h1>
            <SolarSystem />
        </div>
    );
}

export default App;