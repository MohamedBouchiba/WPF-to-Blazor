ALTER TABLE jobs ENABLE ROW LEVEL SECURITY;
ALTER TABLE job_files ENABLE ROW LEVEL SECURITY;
ALTER TABLE job_logs ENABLE ROW LEVEL SECURITY;

CREATE POLICY jobs_user_policy ON jobs
    FOR ALL
    USING (user_id = auth.uid())
    WITH CHECK (user_id = auth.uid());

CREATE POLICY job_files_user_policy ON job_files
    FOR ALL
    USING (job_id IN (SELECT id FROM jobs WHERE user_id = auth.uid()))
    WITH CHECK (job_id IN (SELECT id FROM jobs WHERE user_id = auth.uid()));

CREATE POLICY job_logs_user_policy ON job_logs
    FOR ALL
    USING (job_id IN (SELECT id FROM jobs WHERE user_id = auth.uid()))
    WITH CHECK (job_id IN (SELECT id FROM jobs WHERE user_id = auth.uid()));
