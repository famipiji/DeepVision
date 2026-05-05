import React, { useState, useEffect } from 'react';
import axios from 'axios';
import VaultViewer from './VaultViewer';
import UploadRecord from './UploadRecord';
import { searchElasticsearch } from '../services/api';

// --- ADDED FOR PDF VIEWER ---
import { Worker, Viewer } from '@react-pdf-viewer/core';
import { defaultLayoutPlugin } from '@react-pdf-viewer/default-layout';
import '@react-pdf-viewer/core/lib/styles/index.css';
import '@react-pdf-viewer/default-layout/lib/styles/index.css';
// ----------------------------

const Repository = ({ setBreadcrumbs }) => {
    const [viewMode, setViewMode] = useState('TABLE'); 
    const [results, setResults] = useState([]);
    const [selectedRecord, setSelectedRecord] = useState(null);
    const [searchQuery, setSearchQuery] = useState("");
    const [schema, setSchema] = useState([]);
    const [previewUrl, setPreviewUrl] = useState(null); 

    // --- INITIALIZE PLUGIN ---
    const defaultLayoutPluginInstance = defaultLayoutPlugin();

    // Ingestion State
    const [recordTypes, setRecordTypes] = useState([]);
    const [selectedTypeId, setSelectedTypeId] = useState("");

    // --- PAGNIATION ---
    const [currentPage, setCurrentPage] = useState(1);
    const [pageSize, setPageSize] = useState(10); // Number of rows per page

    // Calculate pagination values
    const totalResults = results.length;
    const totalPages = Math.ceil(totalResults / pageSize);
    const startIndex = (currentPage - 1) * pageSize;
    const paginatedResults = results.slice(startIndex, startIndex + pageSize);

    const [isDetailView, setIsDetailView] = useState(false);
    const [editingRecord, setEditingRecord] = useState(null);

    // Fetch available record types for the Ingestion dropdown
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

    useEffect(() => {
        return () => {
            if (previewUrl) {
                console.log("Cleaning up temporary file URL...");
                URL.revokeObjectURL(previewUrl);
            }
        };
    }, [previewUrl]);

    const handleRowDoubleClick = (record) => {
        setEditingRecord({ 
            ...record, 
            metadata: { ...record.metadata } 
        }); 
        setIsDetailView(true); // Switch to the Detail View mode
    };

    const handleBackToTable = () => {
        setIsDetailView(false);
        setEditingRecord(null);
    };

    const handleSearch = async () => {
        if (!searchQuery.trim()) return;
    
        try {
            const data = await searchElasticsearch(searchQuery);
            setResults(data);
            setSelectedRecord(null);

            if (data.length > 0) {
            let rawSchema = data[0].fieldSchema; //[cite: 2, 3]
            
            // If it's a string, parse it. If it's already an array/object, use it.[cite: 2]
            let parsedSchema = typeof rawSchema === 'string' 
                ? JSON.parse(rawSchema) 
                : rawSchema;

            //const fieldArray = parsedSchema?.fields || (Array.isArray(parsedSchema) ? parsedSchema : []);
            //setSchema(fieldArray);
            setSchema([]);
             
            
            setCurrentPage(1);
        }
        } catch (err) {
            console.error("Search error:", err);
            alert("Search failed. Check API connection.");
        }
    };

    const inputStyle = {
        padding: '10px',
        border: '1px solid #d9d9d9',
        borderRadius: '4px',
        fontSize: '14px',
        width: '100%',
        boxSizing: 'border-box'
    };

    const fetchRecordDetails = async (id) => {
        try {
            const response = await fetch(`https://localhost:7002/api/records/file/${id}`);
            if (!response.ok) throw new Error("Failed to fetch file");

            const blob = await response.blob();
            const url = URL.createObjectURL(blob);
            
            setPreviewUrl(url);
            setViewMode('VIEWER'); // Switch to viewer mode on double-click
        } catch (err) {
            console.error("Error displaying document:", err);
        }
    };

    const toggleIngest = () => {
        setViewMode('INGEST');
        setBreadcrumbs(['Home', 'Repository', 'Ingest']);
    };

    const handleSaveMetadata = async () => {
        try {
            const response = await fetch(`https://localhost:7002/api/Search/update-metadata/${editingRecord.recordId}`, {
                method: 'PUT',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify(editingRecord.metadata) // Sending the edited dictionary
            });

            if (response.ok) {
                alert("Metadata updated successfully!");
                setIsDetailView(false);
                // Refresh your search to see the changes in the table
                handleSearch(); 
            } else {
                alert("Failed to save metadata.");
            }
        } catch (error) {
            console.error("Error saving metadata:", error);
        }
    };

    return (
        <div style={{ display: 'flex', height: '100%', width: '100%', backgroundColor: 'white' }}>
            
            {/* LEFT PANEL: SEARCH & INGEST TOOLS */}
            <div style={{ width: '20%', borderRight: '1px solid #f0f0f0', padding: '20px', display: 'flex', flexDirection: 'column', gap: '20px' }}>
                <div>
                    <h3 style={{ fontSize: '12px', color: '#8c8c8c', margin: '0 0 10px 0', textTransform: 'uppercase' }}>Main Actions</h3>
                    <button 
                        onClick={toggleIngest}
                        style={{ width: '100%', padding: '10px', backgroundColor: viewMode === 'INGEST' ? '#52c41a' : '#f0f0f0', color: viewMode === 'INGEST' ? 'white' : 'black', border: 'none', borderRadius: '4px', cursor: 'pointer', fontWeight: '600', marginBottom: '10px' }}
                    >
                        🚀 Ingest New Record
                    </button>
                    <button 
                        onClick={() => setViewMode('TABLE')}
                        style={{ width: '100%', padding: '10px', backgroundColor: viewMode === 'TABLE' ? '#1890ff' : '#f0f0f0', color: viewMode === 'TABLE' ? 'white' : 'black', border: 'none', borderRadius: '4px', cursor: 'pointer', fontWeight: '600' }}
                    >
                        🔍 Search Records
                    </button>
                </div>

                {viewMode === 'INGEST' && (
                    <div style={{ borderTop: '1px solid #f0f0f0', paddingTop: '15px' }}>
                        <label style={{ fontSize: '11px', fontWeight: '700', color: '#8c8c8c', textTransform: 'uppercase' }}>Active Template</label>
                        <select 
                            style={{ width: '100%', padding: '8px', marginTop: '5px' }}
                            value={selectedTypeId}
                            onChange={(e) => setSelectedTypeId(e.target.value)}
                        >
                            <option value="">-- Select --</option>
                            {recordTypes.map(type => (
                                <option key={type.id} value={type.id}>{type.name}</option>
                            ))}
                        </select>
                    </div>
                )}

                <div style={{ borderTop: '1px solid #f0f0f0', paddingTop: '15px' }}>
                    <h3 style={{ fontSize: '12px', color: '#8c8c8c', margin: '0 0 10px 0', textTransform: 'uppercase' }}>Search Filters</h3>
                    <input 
                        type="text" 
                        value={searchQuery}
                        onChange={(e) => setSearchQuery(e.target.value)}
                        placeholder="Keyword..." 
                        style={{ width: '100%', padding: '8px', border: '1px solid #d9d9d9', marginBottom: '10px' }}
                    />
                    <button onClick={handleSearch} style={{ width: '100%', padding: '8px', backgroundColor: '#1890ff', color: 'white', border: 'none', cursor: 'pointer' }}>
                        Run Search
                    </button>
                </div>
            </div>

            {/* CENTER PANEL */}
            <div style={{ width: '60%', display: 'flex', flexDirection: 'column', borderRight: '1px solid #f0f0f0', backgroundColor: '#fff' }}>
                <div style={{ flex: 1, overflowY: 'auto' }}>
                    
                    {/* VIEW 1: SEARCH TABLE */}
                    {!isDetailView && viewMode === 'TABLE' && (
                        <>
                            <table style={{ width: '100%', borderCollapse: 'collapse', tableLayout: 'fixed' }}>
                                <thead style={{ backgroundColor: '#fafafa', position: 'sticky', top: 0, zIndex: 1 }}>
                                    <tr>
                                        <th style={{ textAlign: 'left', padding: '10px 12px', borderBottom: '1px solid #f0f0f0', width: '75%', fontSize: '12px', color: '#8c8c8c' }}>RECORD</th>
                                        <th style={{ textAlign: 'left', padding: '10px 12px', borderBottom: '1px solid #f0f0f0', width: '25%', fontSize: '12px', color: '#8c8c8c' }}>DATE</th>
                                    </tr>
                                </thead>
                                <tbody style={{ verticalAlign: 'top' }}>
                                    {paginatedResults.map((r) => (
                                        <tr 
                                            key={r.id} 
                                            onClick={() => {
                                                setSelectedRecord(r);
                                                fetchRecordDetails(r.recordId);

                                                // Parse the schema for the specific record clicked
                                                let rawSchema = r.fieldSchema;
                                                let parsedSchema = typeof rawSchema === 'string'
                                                    ? JSON.parse(rawSchema)
                                                    : rawSchema;

                                                const fieldArray = parsedSchema?.fields || (Array.isArray(parsedSchema) ? parsedSchema : []);
                                                setSchema(fieldArray);
                                            }}
                                            onDoubleClick={() => handleRowDoubleClick(r)}
                                            style={{ 
                                                cursor: 'pointer', 
                                                borderBottom: '1px solid #f0f0f0',
                                                backgroundColor: selectedRecord?.id === r.id ? '#e6f7ff' : 'transparent',
                                                height: '70px' 
                                            }}
                                        >
                                            <td style={{ padding: '8px 12px' }}>
                                                <div style={{ fontWeight: '600', color: '#1a0dab', fontSize: '14px' }}>
                                                    {r.fileName || "Unknown Document"}
                                                </div>
                                                <div 
                                                    style={{ 
                                                        fontSize: '12px', color: '#4d5156', marginTop: '2px',
                                                        display: '-webkit-box', WebkitLineClamp: '2', WebkitBoxOrient: 'vertical',
                                                        overflow: 'hidden', lineHeight: '1.4'
                                                    }}
                                                    dangerouslySetInnerHTML={{ __html: r.content || "No matching text..." }}
                                                />
                                            </td>
                                            <td style={{ padding: '8px 12px', fontSize: '12px', color: '#70757a' }}>
                                                {r.createdAt ? new Date(r.createdAt).toLocaleDateString() : '-'}
                                            </td>
                                        </tr>
                                    ))}
                                </tbody>
                            </table>

                            {/* PAGINATION FOOTER - Only shows in Table Mode */}
                            <div style={{ 
                                padding: '10px 20px', borderTop: '1px solid #f0f0f0', display: 'flex', 
                                justifyContent: 'space-between', alignItems: 'center', backgroundColor: '#fafafa',
                                fontSize: '13px', color: '#5f6368', position: 'sticky', bottom: 0 
                            }}>
                                <div>Showing <b>{startIndex + 1}</b> - <b>{Math.min(startIndex + pageSize, totalResults)}</b> of <b>{totalResults}</b></div>
                                <div style={{ display: 'flex', gap: '8px', alignItems: 'center' }}>
                                    <button disabled={currentPage === 1} onClick={() => setCurrentPage(p => p - 1)}>Prev</button>
                                    <span>Page {currentPage} of {totalPages || 1}</span>
                                    <button disabled={currentPage === totalPages} onClick={() => setCurrentPage(p => p + 1)}>Next</button>
                                </div>
                            </div>
                        </>
                    )}

                    {/* VIEW 2: CLEAN DETAIL VIEW (EDIT MODE) */}
                    {isDetailView && (
                        <div style={{ padding: '30px', animation: 'fadeIn 0.2s' }}>
                            <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '30px' }}>
                                <div style={{ display: 'flex', alignItems: 'center', gap: '15px' }}>
                                    <button 
                                        onClick={handleBackToTable}
                                        style={{ padding: '6px 12px', borderRadius: '4px', border: '1px solid #d9d9d9', backgroundColor: '#fff', cursor: 'pointer' }}
                                    >
                                        ← Back
                                    </button>
                                    <h2 style={{ margin: 0, fontSize: '20px' }}>Edit Record Details</h2>
                                </div>
                                <button 
                                    onClick={handleSaveMetadata}
                                    style={{ padding: '8px 20px', backgroundColor: '#1890ff', color: 'white', border: 'none', borderRadius: '4px', cursor: 'pointer', fontWeight: 'bold' }}
                                >
                                    Save Changes
                                </button>
                            </div>

                            <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '20px' }}>
                                {Object.keys(editingRecord?.metadata || {}).map((key) => {
                                    // Find the field definition in the schema to get its type
                                    const fieldDef = schema.find(f => f.name === key);
                                    const fieldType = fieldDef?.type?.toLowerCase() || 'text';

                                    return (
                                        <div key={key} style={{ display: 'flex', flexDirection: 'column', gap: '5px' }}>
                                            <label style={{ fontSize: '11px', color: '#8c8c8c', fontWeight: 'bold', textTransform: 'uppercase' }}>
                                                {fieldDef?.label || key}
                                            </label>

                                            {/* Render different inputs based on the field type[cite: 3, 5] */}
                                            {fieldType === 'date' ? (
                                                <input
                                                    type="date"
                                                    value={editingRecord.metadata[key] || ''}
                                                    onChange={(e) => setEditingRecord({
                                                        ...editingRecord,
                                                        metadata: { ...editingRecord.metadata, [key]: e.target.value }
                                                    })}
                                                    style={inputStyle}
                                                />
                                            ) : fieldType === 'number' ? (
                                                <input
                                                    type="number"
                                                    value={editingRecord.metadata[key] || ''}
                                                    onChange={(e) => setEditingRecord({
                                                        ...editingRecord,
                                                        metadata: { ...editingRecord.metadata, [key]: e.target.value }
                                                    })}
                                                    style={inputStyle}
                                                />
                                            ) : (
                                                // Default to Text for everything else
                                                <input
                                                    type="text"
                                                    value={editingRecord.metadata[key] || ''}
                                                    onChange={(e) => setEditingRecord({
                                                        ...editingRecord,
                                                        metadata: { ...editingRecord.metadata, [key]: e.target.value }
                                                    })}
                                                    style={inputStyle}
                                                />
                                            )}
                                        </div>
                                    );
                                })}
                            </div>
                        </div>
                    )}

                    {/* INGEST VIEW */}
                    {viewMode === 'INGEST' && <UploadRecord definitionId={selectedTypeId} />}
                </div>
            </div>

            {/* RIGHT PANEL: DETAILS */}
            <div style={{ width: '20%', backgroundColor: '#fafafa', padding: '20px', borderLeft: '1px solid #f0f0f0' }}>
                <h3 style={{ fontSize: '12px', color: '#8c8c8c', margin: '0 0 20px 0', textTransform: 'uppercase', letterSpacing: '1px' }}>
                    Record Details
                </h3>
                
                {selectedRecord ? (
                    <div style={{ display: 'flex', flexDirection: 'column', gap: '20px' }}>

                        {/* Document Preview */}
                        {previewUrl && (
                            <div style={{ height: '220px', border: '1px solid #e8e8e8', borderRadius: '4px', overflow: 'hidden', backgroundColor: '#fff' }}>
                                {selectedRecord.fileName?.toLowerCase().endsWith('.pdf') ? (
                                    <Worker workerUrl="https://unpkg.com/pdfjs-dist@3.11.174/build/pdf.worker.min.js">
                                        <Viewer fileUrl={previewUrl} />
                                    </Worker>
                                ) : (
                                    <img src={previewUrl} alt="Preview" style={{ width: '100%', height: '100%', objectFit: 'contain' }} />
                                )}
                            </div>
                        )}

                        {/* Standard Info[cite: 8] */}
                        <div>
                            <label style={{ fontSize: '11px', color: '#8c8c8c', fontWeight: 'bold' }}>FILE NAME</label>
                            <div style={{ fontSize: '13px', fontWeight: '600', marginTop: '4px', wordBreak: 'break-all' }}>
                                {selectedRecord.fileName}
                            </div>
                        </div>

                        {/* Dynamic Schema Fields (Now only in the Right Panel)[cite: 8] */}
                        {schema.map(f => (
                            
                            <div key={`detail-${selectedRecord.id}-${f.name}`}>
                                
                                <label style={{ fontSize: '11px', color: '#8c8c8c', fontWeight: 'bold' }}>
                                    {(f.label || f.name).toUpperCase()}
                                </label>
                                <div style={{ fontSize: '13px', fontWeight: '600', marginTop: '4px' }}>
                                    {selectedRecord.metadata?.[f.name] || 'N/A'}
                                </div>
                            </div>
                        ))}

                        <div style={{ marginTop: '10px', paddingTop: '15px', borderTop: '1px solid #e8e8e8' }}>
                            <label style={{ fontSize: '11px', color: '#8c8c8c', fontWeight: 'bold' }}>MATCHED PAGE</label>
                            <div style={{ fontSize: '13px', fontWeight: '600' }}>Page {selectedRecord.pageNumber}</div>
                        </div>

                    </div>
                ) : (
                    <div style={{ textAlign: 'center', color: '#bfbfbf', marginTop: '100px' }}>
                        <div style={{ fontSize: '24px', marginBottom: '10px' }}>📄</div>
                        <p style={{ fontSize: '12px' }}>Select a result to view full metadata</p>
                    </div>
                )}
            </div>

            
        </div>
    );
};

export default Repository;