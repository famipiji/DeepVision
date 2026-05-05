import React, { useState, useEffect } from 'react';
import axios from 'axios';

const UploadRecord = ({ definitionId }) => {
    const [definition, setDefinition] = useState(null);
    const [fields, setFields] = useState([]);
    const [formData, setFormData] = useState({});
    const [file, setFile] = useState(null);
    const [loading, setLoading] = useState(false);

    useEffect(() => {
        const fetchDefinition = async () => {
            try {
                const response = await axios.get(`/api/RecordDefinitions/${definitionId}`);
                setDefinition(response.data);

                // PARSE THE JSON
                const schemaObj = typeof response.data.fieldSchema === 'string' 
                    ? JSON.parse(response.data.fieldSchema) 
                    : response.data.fieldSchema;

                // FIX: Set 'fields' to the array INSIDE the object, not the object itself
                // If the schema is {"fields": [...]}, we want schemaObj.fields
                setFields(schemaObj.fields || []); 
                
            } catch (err) {
                console.error("Error fetching record definition:", err);
                setFields([]); // Fallback to empty array to prevent map error
            }
        };

        if (definitionId) fetchDefinition();
    }, [definitionId]);

    const handleInputChange = (e) => {
        const { name, value } = e.target;
        setFormData(prev => ({ ...prev, [name]: value }));
    };

    const handleSubmit = async (e) => {
        e.preventDefault();
        setLoading(true);

        const data = new FormData();
        data.append('recordDefinitionId', definitionId);
        if (file) data.append('file', file);

        // Use EXACT lowercase 'metadata' to match the FromForm attribute above
        const metadataJson = JSON.stringify(formData);
        console.log("Sending Metadata:", metadataJson); // Check your browser console to see this!
        data.append('metadata', metadataJson);

        // FIX: Stringify the entire object into one field named 'Metadata'
        data.append('Metadata', JSON.stringify(formData));

        try {
            const response = await axios.post('/api/Records/upload', data, {
                headers: { 'Content-Type': 'multipart/form-data' }
            });

            const createdId = response.data.id || response.data.Id;

            if (createdId) {
                alert(`Success! Record Created with ID: ${createdId}`);
                setFormData({}); 
                // Optional: reset file input here too
            } else {
                alert("Success! Record created, but ID was not returned.");
            }
        } catch (err) {
            alert("Upload failed. Check console for details.");
            console.error(err);
        } finally {
            setLoading(false);
        }
    };

    if (!definition) return <div>Select a record type to begin...</div>;

    return (
        <div className="card border-0 shadow-sm">
            <div className="card-header bg-primary text-white py-3">
                <h5 className="mb-0">📥 Ingest New {definition.name}</h5>
            </div>
            <div className="card-body p-4">
                <form onSubmit={handleSubmit}>
                    <div className="row">
                        {fields.map((f) => (
                            /* Change f.field to f.name here */
                            <div key={f.name} className="col-md-6 mb-3">
                                <label className="form-label fw-semibold text-secondary small uppercase">
                                    {f.label || f.name}
                                </label>
                                <input
                                    type={f.type === 'date' ? 'date' : f.type === 'number' ? 'number' : 'text'}
                                    name={f.name}
                                    className="form-control form-control-lg border-2"
                                    placeholder={`Enter ${(f.label || f.name).toLowerCase()}...`}
                                    required
                                    onChange={handleInputChange}
                                />
                            </div>
                        ))}
                    </div>

                    <div className="mb-4 mt-2">
                        <label className="form-label fw-bold">Document Attachment</label>
                        <div className="input-group">
                            <input 
                                type="file" 
                                className="form-control" 
                                id="inputGroupFile04" 
                                onChange={(e) => setFile(e.target.files[0])} 
                            />
                        </div>
                        <div className="form-text">Files are encrypted and stored in SeaweedFS.</div>
                    </div>

                    <div className="d-grid">
                        <button 
                            type="submit" 
                            className="btn btn-primary btn-lg shadow-sm" 
                            disabled={loading}
                        >
                            {loading ? (
                                <>
                                    <span className="spinner-border spinner-border-sm me-2"></span>
                                    Encrypting & Uploading...
                                </>
                            ) : "🔒 Encrypt & Store Record"}
                        </button>
                    </div>
                </form>
            </div>
        </div>
    );
};

export default UploadRecord;