import React, { useEffect, useState } from 'react';
import { Worker, Viewer } from '@react-pdf-viewer/core';
import { defaultLayoutPlugin } from '@react-pdf-viewer/default-layout';

// Import the styles
import '@react-pdf-viewer/core/lib/styles/index.css';
import '@react-pdf-viewer/default-layout/lib/styles/index.css';

const VaultViewer = ({ recordId }) => {
    const [data, setData] = useState(null);
    const [error, setError] = useState(null);
    
    // Initialize the plugin for the toolbar/sidebar
    const defaultLayoutPluginInstance = defaultLayoutPlugin();

    useEffect(() => {
        // Replace with your actual .NET API port if different
        fetch(`https://localhost:7002/api/records/${recordId}`)
            .then(res => {
                if (!res.ok) throw new Error('Could not unlock record.');
                return res.json();
            })
            .then(json => setData(json))
            .catch(err => setError(err.message));
    }, [recordId]);

    if (error) return <div style={{ color: 'red' }}>Error: {error}</div>;
    if (!data) return <div>Unlocking iVault Record...</div>;

    return (
        <div style={{ display: 'flex', height: '100vh', border: '1px solid rgba(0, 0, 0, 0.3)' }}>
            {/* Sidebar for Decrypted Metadata */}
            <div style={{ width: '250px', padding: '15px', borderRight: '1px solid #ccc', overflowY: 'auto' }}>
                <h4>Decrypted Metadata</h4>
                <hr />
                {data.metadata && Object.entries(data.metadata).map(([key, value]) => (
                    <div key={key} style={{ marginBottom: '15px' }}>
                        <label style={{ fontSize: '12px', color: '#666', display: 'block' }}>{key.toUpperCase()}</label>
                        <strong style={{ wordBreak: 'break-all' }}>{value}</strong>
                    </div>
                ))}
            </div>

            {/* Main Content: PDF Viewer */}
            <div style={{ flex: 1 }}>
                {data.fileUrl ? (
                    <Worker workerUrl={`https://unpkg.com/pdfjs-dist@3.11.174/build/pdf.worker.min.js`}>
                        <Viewer 
    fileUrl={`https://localhost:7002/api/records/file/${recordId}`} 
    plugins={[defaultLayoutPluginInstance]} 
/>
                    </Worker>
                ) : (
                    <div style={{ padding: '20px' }}>No PDF found for this record.</div>
                )}
            </div>
        </div>
    );
};

export default VaultViewer;