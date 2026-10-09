DELETE FROM
    DeviceEnrollments
WHERE
    Method = 'PairingCode'
    AND ExpiresAt < /*@ before */''
