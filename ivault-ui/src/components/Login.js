import React, { useState } from 'react';
import axios from 'axios';

const Login = ({ onLoginSuccess }) => {
    const [Username, setUsername] = useState('');
    const [Password, setPassword] = useState('');
    const [error, setError] = useState('');

    const handleLogin = async (e) => {
        e.preventDefault();
        try {
            // This calls the API you just tested in Swagger!
            const response = await axios.post('https://localhost:7002/api/Auth/login', {
                username: Username,
                passwordHash: Password // Matching our API's model name
            });

            const token = response.data.token;
            localStorage.setItem('token', token); // Save the badge in the browser
            //alert('Login Successful! Token saved.');
            //console.log("Login call worked, about to trigger the redirect...");
            onLoginSuccess();
            // Later, we will redirect the user to the Dashboard here
        } catch (err) {
            setError('Invalid username or password');
        }
    };

    return (
        <div style={{ padding: '50px', textAlign: 'center' }}>
            <h2>iVault Plus Login</h2>
            <form onSubmit={handleLogin}>
                <input  
                    type="text" 
                    placeholder="Username" 
                    onChange={(e) => setUsername(e.target.value)} 
                    style={{ display: 'block', margin: '10px auto' }}
                />
                <input 
                    type="password" 
                    placeholder="Password" 
                    onChange={(e) => setPassword(e.target.value)} 
                    style={{ display: 'block', margin: '10px auto' }}
                />
                <button type="submit">Login</button>
            </form>
            {error && <p style={{ color: 'red' }}>{error}</p>}
        </div>
    );
};

export default Login;