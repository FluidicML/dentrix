# Dentrix Code Signing

## Quickstart

Push to the `dtx-sql-browser` branch to produce a new signed version of the SQL
browser. You can zip the produced `DtxSQLBrowser.exe` file and send to Dentrix
(through their developer portal) for them to extract the public key and add it
to their servers. Henry Schein One should send a notification to us when that
is finished.

## Code Signing Certificate Creation

Integrating with Dentrix is painful. The only way to do it, even in a
development environment, is to create a signed Windows application. Signing
requires a certificate that:

> must contain a public key with RSA encryption of at least RSA 2048 Bits and
> it must be issued by a CA or a Trusted Authority that is approved by
> Microsoft. (Digicert, SSL.com, GlobalSign, Sectigo (Comodo), etc...)

We ended up using DigiCert and its unlikely we'll ever switch away considering
how tedious the organization validation is (especially EV validation we opted
into). When our certificate expires, we'll need to either renew it or create a
new one. In most cases we probably just want to renew and not think about it
any further.

### Azure

Navigate to the `kv-dentrix-shared-eastus` Key Vault in Azure. In the sidebar,
click on `Objects > Certificates`. Afterwards, press `Generate/Import`. Fill
out the following:

![Screenshot from 2024-11-08 10-10-22](https://github.com/user-attachments/assets/933b7a54-f027-4efd-92cb-21a7c09af223)

Field | Value
----- | -----
Method of Certificate Creation | Generate
Type of Certificate Authority (CA) | Certificate issued by a non-integrated CA
Subject | CN=fluidicml.com, O="Fluidic ML, INC.", L=Fremont, S=California, C=US
Content Type | PKCS #12

Next, click `Not configured` under `Advanced Policy Configuration` and enter
the following details:

Field | Value
----- | -----
Extended Key Usages (EKUs) | 1.3.6.1.5.5.7.3.1, 1.3.6.1.5.5.7.3.2, 1.3.6.1.5.5.7.3.3, 1.3.6.1.4.1.311.10.3.13
X.509 Key Usage Flags | Digital Signature, Key Encipherment
Exportable Private Key? | No
Key Type | RSA-HSM
Key Size | 3072
Enable Certificate Transparency? | No
Certificate Type | *Leave Blank*

For any other specified form fields, it is up to you what to choose. Just make
sure the values align with what DigiCert also asks.

---

Once finished, click on the newly created entry in the table. Press
`Certificate Operation` and download the CSR. With this in hand, you can now go
to DigiCert.

### DigiCert

Once logged in, select `Certificates > Orders` in the sidebar. On the new page,
press `Request a certificate > EV Code Signing`. Fill out the following:

Field | Value
----- | -----
Validity Period | *Matches value in Azure*
Organization | Fluidic ML, INC.
Provisioning options | Install on an HSM
FIPS 140-2 level 2 HSM | Yes

You can now upload the CSR downloaded from Azure. Once the order has been made,
the configured recipient will get an email to approve. The certificate should
be issued shortly after.

### Final Steps

Unzip the emailed bundle of certificates. Go back to the created certificate
entry and click `Certificate Operations`. This time you'll click `Merge Signed
Request`. Specify the organization specific certificate to finish the process.

Make sure to configure the GitHub Actions secrets to reflect this completed
certificate.
