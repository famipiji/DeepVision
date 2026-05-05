import React, { useState, useEffect } from 'react';
import axios from 'axios';
import UploadRecord from './UploadRecord'; // Keeping your existing logic
import Repository from './Repository';


const Dashboard = () => {
    // --- Existing States & Logic ---
    const [recordTypes, setRecordTypes] = useState([]);
    const [selectedTypeId, setSelectedTypeId] = useState("");
    const [showUpload, setShowUpload] = useState(false);

    // --- New Layout States (Step 1) ---
    const [activeTab, setActiveTab] = useState('WORKSPACE'); // 'WORKSPACE' or 'REPOSITORY'
    const [isProfileOpen, setIsProfileOpen] = useState(false);
    const [currentPath, setCurrentPath] = useState(['Home', 'Workspace']);

    useEffect(() => {
        const fetchTypes = async () => {
            try {
                const res = await axios.get('/api/recorddefinitions');
                setRecordTypes(res.data);
            } catch (err) {
                console.error("Failed to load record types", err);
            }
        };
        fetchTypes();
    }, []);

    const handleLogout = () => {
        localStorage.removeItem('token');
        window.location.reload(); 
    };

    const navigateTo = (tab) => {
        setActiveTab(tab);
        setCurrentPath(['Home', tab.charAt(0) + tab.slice(1).toLowerCase()]);
    };

    return (
        <div style={{ display: 'flex', flexDirection: 'column', height: '100vh', width: '100vw', overflow: 'hidden', backgroundColor: '#f5f7f9', fontFamily: 'Segoe UI, Tahoma, Geneva, Verdana, sans-serif' }}>
            
            {/* 1. GLOBAL HEADER */}
            <header style={{ height: '48px', backgroundColor: '#001529', color: 'white', display: 'flex', alignItems: 'center', justifyContent: 'space-between', padding: '0 20px', flexShrink: 0, boxShadow: '0 2px 4px rgba(0,0,0,0.1)' }}>
                <div style={{ display: 'flex', alignItems: 'center' }}>
                    <span style={{ fontWeight: 'bold', fontSize: '20px', letterSpacing: '0.5px' }}>
                        <span style={{ color: '#1890ff' }}>i</span>VaultECM
                    </span>
                </div>

                {/* Avatar & Profile Dropdown */}
                <div style={{ position: 'relative' }}>
                    <div 
                        onClick={() => setIsProfileOpen(!isProfileOpen)}
                        style={{ width: '32px', height: '32px', borderRadius: '50%', backgroundColor: '#1890ff', cursor: 'pointer', display: 'flex', alignItems: 'center', justifyContent: 'center', fontWeight: 'bold', fontSize: '14px', transition: 'background 0.3s' }}
                    >
                        U
                    </div>
                    {isProfileOpen && (
                        <div style={{ position: 'absolute', right: 0, top: '42px', backgroundColor: 'white', border: '1px solid #d9d9d9', borderRadius: '4px', padding: '8px 0', zIndex: 1000, boxShadow: '0 3px 6px -4px rgba(0,0,0,0.12), 0 6px 16px 0 rgba(0,0,0,0.08), 0 9px 28px 8px rgba(0,0,0,0.05)', minWidth: '120px' }}>
                            <div style={{ padding: '8px 16px', fontSize: '14px', color: '#000000d9', borderBottom: '1px solid #f0f0f0', marginBottom: '4px' }}>User Admin</div>
                            <button 
                                onClick={handleLogout} 
                                style={{ width: '100%', border: 'none', background: 'none', textAlign: 'left', padding: '8px 16px', cursor: 'pointer', color: '#ff4d4f', fontSize: '14px' }}
                            >
                                Logout
                            </button>
                        </div>
                    )}
                </div>
            </header>

            {/* 2. NAVIGATION PANEL */}
            <nav style={{ height: '42px', backgroundColor: 'white', borderBottom: '1px solid #d9d9d9', display: 'flex', alignItems: 'center', padding: '0 10px', flexShrink: 0 }}>
                <button 
                    onClick={() => navigateTo('WORKSPACE')}
                    style={{ margin: '0 15px', border: 'none', background: 'none', color: activeTab === 'WORKSPACE' ? '#1890ff' : '#595959', borderBottom: activeTab === 'WORKSPACE' ? '2px solid #1890ff' : '2px solid transparent', height: '100%', cursor: 'pointer', fontWeight: activeTab === 'WORKSPACE' ? '600' : '400', transition: 'all 0.2s' }}
                >
                    Workspace
                </button>
                <button 
                    onClick={() => navigateTo('REPOSITORY')}
                    style={{ margin: '0 15px', border: 'none', background: 'none', color: activeTab === 'REPOSITORY' ? '#1890ff' : '#595959', borderBottom: activeTab === 'REPOSITORY' ? '2px solid #1890ff' : '2px solid transparent', height: '100%', cursor: 'pointer', fontWeight: activeTab === 'REPOSITORY' ? '600' : '400', transition: 'all 0.2s' }}
                >
                    Repository
                </button>
            </nav>

            {/* 3. BREADCRUMB PANEL */}
            <div style={{ height: '32px', backgroundColor: '#fafafa', borderBottom: '1px solid #f0f0f0', display: 'flex', alignItems: 'center', padding: '0 24px', fontSize: '13px', color: '#8c8c8c', flexShrink: 0 }}>
                {currentPath.map((item, index) => (
                    <span key={index}>
                        {item} {index < currentPath.length - 1 && <span style={{ margin: '0 8px' }}>/</span>}
                    </span>
                ))}
            </div>

            {/* 4. MAIN WORK AREA (The Workspace) */}
            <main style={{ flexGrow: 1, overflow: 'hidden', position: 'relative' }}>
                {activeTab === 'WORKSPACE' ? (
                    /* Existing Dashboard logic stays here for now */
                    <div style={{ padding: '24px', height: '100%', overflowY: 'auto' }}>
                         <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(240px, 1fr))', gap: '24px' }}>
                            <div className="card shadow-sm p-4 text-center bg-white border-0">
                                <h5 className="text-muted">Documents Indexed</h5>
                                <h2 className="text-primary">1,248</h2>
                            </div>
                            <div className="card shadow-sm p-4 text-center bg-white border-0">
                                <h5 className="text-muted">Storage Usage</h5>
                                <h2 className="text-success">14.2 GB</h2>
                            </div>
                            <div className="card shadow-sm p-4 text-center bg-white border-0">
                                <h5 className="text-muted">System Health</h5>
                                <h2 className="text-info">Stable</h2>
                            </div>
                         </div>
                    </div>
                ) : (
                    <Repository setBreadcrumbs={setCurrentPath} />
                )}
            </main>
        </div>
    );
};

export default Dashboard;