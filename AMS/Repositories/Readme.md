CREATE TABLE t_employee (
    c_empid INT PRIMARY KEY GENERATED ALWAYS AS IDENTITY( INCREMENT 1 START 1 MINVALUE 1 MAXVALUE 2147483647 CACHE 1 ),
    c_name VARCHAR(100) NOT NULL,
    c_email VARCHAR(150) UNIQUE NOT NULL,
    c_password VARCHAR(255) NOT NULL,

    c_gender VARCHAR(10) CHECK (c_gender IN ('Male','Female','Other')),

    c_status VARCHAR(10) DEFAULT 'Inactive'
        CHECK (c_status IN ('Inactive','Active')),

    c_image VARCHAR(255),

    c_role VARCHAR(10) DEFAULT 'Employee'
        CHECK (c_role IN ('Admin','Employee'))
);

CREATE TABLE t_attendance (
    c_attendid INT PRIMARY KEY GENERATED ALWAYS AS IDENTITY( INCREMENT 1 START 1 MINVALUE 1 MAXVALUE 2147483647 CACHE 1 ),

    c_empid INT NOT NULL,

    c_attenddate DATE NOT NULL,

    c_clockinhour INT,
    c_clockinmin INT,

    c_clockouthour INT,
    c_clockoutmin INT,

    c_workinghour INT,

    c_attendstatus VARCHAR(10) DEFAULT 'Regular'
        CHECK (c_attendstatus IN ('LateIn','EarlyOut','Regular')),

    c_worktype VARCHAR(10)
        CHECK (c_worktype IN ('Remote','Office','Field')),

    c_tasktype VARCHAR(100),

    FOREIGN KEY (c_empid) REFERENCES t_employees(c_empid)
);

Connection String : "Host=ep-quiet-salad-a18bc26t-pooler.ap-southeast-1.aws.neon.tech; Database=ams; Username=neondb_owner;  Password=npg_Ut9bmi0VRwyJ; SSL Mode=VerifyFull; Channel Binding=Require;"
