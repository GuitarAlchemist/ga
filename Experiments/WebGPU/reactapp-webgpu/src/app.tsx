import SolarSystemSwitcher from "./components/SolarSystemSwitcher";
import './App.css';

function App() {
    return (
        <div className="App">
            <h1 style={{
                color: '#fff',
                margin: '0 0 20px 0',
                width: '100%',
                textAlign: 'center'
            }}>WebGPU Solar System</h1>
            <div className="canvas-container">
                <SolarSystemSwitcher />
            </div>
        </div>
    );
}

export default App;