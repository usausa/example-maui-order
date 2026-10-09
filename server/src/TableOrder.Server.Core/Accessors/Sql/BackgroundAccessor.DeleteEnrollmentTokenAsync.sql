DELETE FROM
    DeviceEnrollments
WHERE
    Method = 'EnrollmentToken'
    AND (ExpiresAt < /*@ before */'' OR RevokedAt < /*@ before */'')
