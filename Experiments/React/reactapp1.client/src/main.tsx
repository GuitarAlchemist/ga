import {StrictMode} from 'react'
import {createRoot} from 'react-dom/client'
import App from './App';
import './index.css'
import store from './app/store'
import {Provider} from 'react-redux'

const root = createRoot(document.getElementById('root')!)
root.render(
    <StrictMode>
        <Provider store={store}>
            <App/>
        </Provider>
    </StrictMode>,
);