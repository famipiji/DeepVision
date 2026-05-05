import React, { useState, useEffect } from 'react';
import Login from './components/Login';
import Dashboard from './components/Dashboard';
import VaultViewer from './components/VaultViewer';

function App() {
  const [isAuthenticated, setIsAuthenticated] = useState(false);
  
  // This is your specific test record GUID
  const testRecordId = "b3a850c1-22e9-497d-a59a-f2cee3ef633f";

  // Check for token when the app loads
  useEffect(() => {
    const token = localStorage.getItem('token');
    if (token) {
      setIsAuthenticated(true);
    }
  }, []);

  const handleLoginSuccess = () => {
    setIsAuthenticated(true);
  };

  const handleLogout = () => {
    localStorage.removeItem('token'); 
    setIsAuthenticated(false);        
  };

  return (
    <div className="App">
      {!isAuthenticated ? (
        <Login onLoginSuccess={handleLoginSuccess} />
      ) : (
        <div className="secure-layout">
          {/* Only the Dashboard is needed now. 
              The Repository and Viewer are already inside it! */}
          <Dashboard onLogout={handleLogout} />
        </div>
      )}
    </div>
  );
}

export default App;