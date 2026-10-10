SELECT
    *
FROM
    Devices
WHERE
    PublicKey = /*@ publicKey */''
    AND IsActive = 1
